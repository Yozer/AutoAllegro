﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using AutoAllegro.Helpers.Extensions;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SoaAllegroService;

namespace AutoAllegro.Services
{
    public class AllegroService : IAllegroService
    {
        private const int CountryCode = 1;
        public TimeSpan WebApiKeyExpirationTime = TimeSpan.FromMinutes(55);

        private readonly IMemoryCache _memoryCache;
        private readonly servicePort _servicePort;
        private readonly ILogger<AllegroService> _logger;

        private string _sessionKey;
        private string _userId;
        private AllegroCredentials _credentials;

        public bool IsLogged => !IsLoginRequired();

        public AllegroService(IMemoryCache memoryCache, servicePort servicePort, ILogger<AllegroService> logger)
        {
            _memoryCache = memoryCache;
            _servicePort = servicePort;
            _logger = logger;
        }

        private bool IsLoginRequired()
        {
            return _userId == null || !_memoryCache.TryGetValue(_userId, out _sessionKey);
        }

        public async Task Login(string userId, AllegroCredentials credentials)
        {
            _userId = userId;
            _credentials = credentials;

            if (_memoryCache.TryGetValue(userId, out _sessionKey))
                return;

            var sysStatus = await _servicePort.doQuerySysStatusAsync(new doQuerySysStatusRequest(1, CountryCode, credentials.ApiKey));
            var loginResult = await _servicePort.doLoginEncAsync(new doLoginEncRequest
            {
                countryCode = CountryCode,
                webapiKey = credentials.ApiKey,
                userHashPassword = credentials.Pass,
                userLogin = credentials.UserName,
                localVersion = sysStatus.verKey,
            });

            _sessionKey = loginResult.sessionHandlePart;
            _memoryCache.Set(userId, _sessionKey, WebApiKeyExpirationTime);
        }

        private async Task<T> DoRequest<T>(Func<Task<T>> request)
        {
            _logger.LogInformation(1, $"Allegro request for: {_userId}. Request type: {typeof(T)}.");
            T response;
            try
            {
                response = await request();
            }
            catch (FaultException e) when (e.Code.Name == "ERR_NO_SESSION" || e.Code.Name == "ERR_SESSION_EXPIRED")
            {
                _logger.LogError(3, e, $"Allegro request for: {_userId}. Invalid session.");
                _memoryCache.Remove(_userId);
                await Login(_userId, _credentials);
                return await DoRequest(request);
            }
            catch (FaultException e)
            {
                _logger.LogError(3, e, $"Unhandled error Allegro request for: {_userId}. {e.Code.Name}");
                throw;
            }

            _logger.LogInformation(2, $"Allegro request for: {_userId}. Success.");
            return response;
        }
        public async Task<List<NewAuction>> GetNewAuctions()
        {
            ThrowIfNotLogged();

            var auctions = await DoRequest(() => _servicePort.doGetMySellItemsAsync(new doGetMySellItemsRequest
            {
                pageSize = 1000,
                sessionId = _sessionKey
            }));

            return auctions.sellItemsList.Select(t => new NewAuction
            {
                Id = t.itemId,
                Name = t.itemTitle,
                StartDate = t.itemStartTime.ToDateTime(),
                EndDate = t.itemEndTime.ToDateTime(),
                Price = Convert.ToDecimal(t.itemPrice[0].priceValue)
            }).ToList();
        }

        public async Task<Auction> UpdateAuctionFees(Auction auction)
        {
            ThrowIfNotLogged();

            var billing = await DoRequest(() => _servicePort.doMyBillingItemAsync(new doMyBillingItemRequest
            {
                itemId = auction.AllegroAuctionId,
                sessionHandle = _sessionKey
            }));

            auction.Fee = billing.endingFees.Sum(t => -decimal.Parse(t.biValue, CultureInfo.InvariantCulture));
            auction.OpenCost = billing.entryFees.Sum(t => -decimal.Parse(t.biValue, CultureInfo.InvariantCulture));
            return auction;
        }

        public IEnumerable<SiteJournalDealsStruct> FetchJournal(long journalStart)
        {
            ThrowIfNotLogged();
            SiteJournalDealsStruct[] response;

            do
            {
                response = DoRequest(() => _servicePort.doGetSiteJournalDealsAsync(new doGetSiteJournalDealsRequest
                {
                    sessionId = _sessionKey,
                    journalStart = journalStart
                    
                })).Result.siteJournalDeals;

                foreach (var dealsStruct in response)
                    yield return dealsStruct;

                if (response.Length > 0)
                {
                    journalStart = response.Last().dealEventId;
                }

            } while (response.Length == 100);
        }

        public Buyer FetchBuyerData(long dealItemId, long dealBuyerId)
        {
            ThrowIfNotLogged();

            var response = DoRequest(() => _servicePort.doGetPostBuyDataAsync(new doGetPostBuyDataRequest
            {
                sessionHandle = _sessionKey,
                buyerFilterArray = new[] {dealBuyerId},
                itemsArray = new[] {dealItemId}
            })).Result.itemsPostBuyData[0].usersPostBuyData[0];

            return new Buyer
            {
                AllegroUserId = response.userData.userId,
                UserLogin = response.userData.userLogin,
                FirstName = response.userData.userFirstName,
                LastName = response.userData.userLastName,
                PostCode = response.userData.userPostcode,
                City = response.userData.userCity,
                Address = response.userData.userAddress,
                Email = response.userData.userEmail,
                Phone = response.userData.userPhone,
                Phone2 = response.userData.userPhone2,
            };
        }

        public Transaction GetTransactionDetails(long dealTransactionId, Order order)
        {
            ThrowIfNotLogged();

            var response = DoRequest(() => _servicePort.doGetPostBuyFormsDataForSellersAsync(new doGetPostBuyFormsDataForSellersRequest
            {
                sessionId = _sessionKey,
                transactionsIdsArray = new[] {dealTransactionId}
            })).Result.postBuyFormData[0];

            var transaction = new Transaction
            {
                TransactionStatus = TransactionStatus.Created,
                Amount = Convert.ToDecimal(response.postBuyFormPaymentAmount),
                AllegroTransactionId = dealTransactionId
            };
            // TODO this probably should be assigned to transaction and only one transaction in order can be completed
            order.ShippingAddress = new ShippingAddress
            {
                Address = response.postBuyFormShipmentAddress.postBuyFormAdrStreet,
                City = response.postBuyFormShipmentAddress.postBuyFormAdrCity,
                FirstName = response.postBuyFormShipmentAddress.postBuyFormAdrFullName.Split(' ')[0],
                LastName = string.Join(" ", response.postBuyFormShipmentAddress.postBuyFormAdrFullName.Split(' ').Skip(1)),
                PostCode = response.postBuyFormShipmentAddress.postBuyFormAdrPostcode,
                MessageToSeller = response.postBuyFormMsgToSeller
            };

            return transaction;
        }
        public async Task<int> SendRefund(Order order, int reasonId)
        {
            ThrowIfNotLogged();
            var response = await DoRequest(() => _servicePort.doSendRefundFormAsync(new doSendRefundFormRequest
            {
                reasonId = reasonId,
                dealId = (int) order.AllegroDealId,
                refundQuantity = order.Quantity,
                sessionId = _sessionKey
            }));

            return response.refundId;
        }
        public async Task<bool> CancelRefund(int refundId)
        {
            ThrowIfNotLogged();
            try
            {
                var response = await DoRequest(() => _servicePort.doCancelRefundFormAsync(new doCancelRefundFormRequest {sessionId = _sessionKey, refundId = refundId}));
                if(response.cancellationResult)
                    return true;
            }
            catch (FaultException)
            {
                // this refund is finished user got warning
            }

            return false;
        }
        public IEnumerable<WaitFeedbackStruct> GetWaitingFeedback()
        {
            ThrowIfNotLogged();

            const int packageSize = 100;
            int offset = 0;
            WaitFeedbackStruct[] response;

            do
            {
                response = DoRequest(() => _servicePort.doGetWaitingFeedbacksAsync(new doGetWaitingFeedbacksRequest(_sessionKey, offset, packageSize))).Result.feWaitList;

                foreach (var feedbackStruct in response)
                    yield return feedbackStruct;

                ++offset;
            } while (response.Length == packageSize);

        }
        public int GivePositiveFeedback(long adId, int userId)
        {
            ThrowIfNotLogged();

            return DoRequest(() => _servicePort.doFeedbackAsync(new doFeedbackRequest
            {
                sessionHandle = _sessionKey,
                feItemId = adId,
                feToUserId = userId,
                feOp = 2,
                feUseCommentTemplate = 1,
                feCommentType = "POS",
                feComment = "Transakcja przebiegła pomyślnie."
            })).Result.feedbackId;

        }
        private void ThrowIfNotLogged()
        {
            if(!IsLogged)
                throw new InvalidOperationException("Not logged in");
        }
    }

    public class AllegroCredentials
    {
        public string UserName { get; }
        public string Pass { get; }
        public string ApiKey { get; }
        public long JournalStart { get; }

        public AllegroCredentials(string userName, string pass, string apiKey, Event journalStart)
        {
            UserName = userName;
            Pass = pass;
            ApiKey = apiKey;
            JournalStart = journalStart?.AllegroEventId ?? 0L;
        }
    }
}