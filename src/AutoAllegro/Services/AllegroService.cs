using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoAllegro.Helpers.Extensions;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using SoaAllegroService;

namespace AutoAllegro.Services
{
    public class AllegroService : IAllegroService
    {
        private const int CountryCode = 1;
        public TimeSpan WebApiKeyExpirationTime = TimeSpan.FromMinutes(55);

        private readonly IMemoryCache _memoryCache;
        private readonly servicePort _servicePort;

        private string _sessionKey;
        public bool IsLogged => _sessionKey != null;

        public AllegroService(IMemoryCache memoryCache, servicePort servicePort)
        {
            _memoryCache = memoryCache;
            _servicePort = servicePort;
        }

        public async Task Login(string userId, Func<AllegroCredentials> getAllegroCredentials)
        {
            if (_memoryCache.TryGetValue(userId, out _sessionKey))
                return;

            var credentials = await Task.Run(getAllegroCredentials);
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
        public async Task<List<NewAuction>> GetNewAuctions()
        {
            ThrowIfNotLogged();

            var auctions = await _servicePort.doGetMySellItemsAsync(new doGetMySellItemsRequest
            {
                pageSize = 1000,
                sessionId = _sessionKey
            });

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

            var billing = await _servicePort.doMyBillingItemAsync(new doMyBillingItemRequest
            {
                itemId = auction.AllegroAuctionId,
                sessionHandle = _sessionKey
            });

            auction.Fee = billing.endingFees.Sum(t => -decimal.Parse(t.biValue));
            auction.OpenCost = billing.entryFees.Sum(t => -decimal.Parse(t.biValue));
            return auction;
        }

        public IEnumerable<SiteJournalDealsStruct> FetchJournal(long journalStart)
        {
            ThrowIfNotLogged();
            SiteJournalDealsStruct[] response;

            do
            {
                response = _servicePort.doGetSiteJournalDealsAsync(new doGetSiteJournalDealsRequest
                {
                    sessionId = _sessionKey,
                    journalStart = journalStart
                    
                }).Result.siteJournalDeals;

                foreach (var dealsStruct in response)
                    yield return dealsStruct;

            } while (response.Length == 100);
        }

        public Buyer FetchBuyerData(long dealItemId, long dealBuyerId)
        {
            ThrowIfNotLogged();

            var response = _servicePort.doGetPostBuyDataAsync(new doGetPostBuyDataRequest
            {
                sessionHandle = _sessionKey,
                buyerFilterArray = new[] {dealBuyerId},
                itemsArray = new[] {dealItemId}
            }).Result.itemsPostBuyData[0].usersPostBuyData[0];

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

            var response = _servicePort.doGetPostBuyFormsDataForSellersAsync(new doGetPostBuyFormsDataForSellersRequest
            {
                sessionId = _sessionKey,
                transactionsIdsArray = new[] {dealTransactionId}
            }).Result.postBuyFormData[0];

            var transaction = new Transaction
            {
                TransactionStatus = TransactionStatus.Created,
                Amount = Convert.ToDecimal(response.postBuyFormPaymentAmount),
                Order = order,
                AllegroTransactionId = dealTransactionId
            };

            order.ShippingAddress = new ShippingAddress
            {
                Address = response.postBuyFormShipmentAddress.postBuyFormAdrStreet,
                City = response.postBuyFormShipmentAddress.postBuyFormAdrCity,
                FirstName = response.postBuyFormShipmentAddress.postBuyFormAdrFullName.Split(' ')[0],
                LastName = string.Join(" ", response.postBuyFormShipmentAddress.postBuyFormAdrFullName.Split(' ').Skip(1)),
                PostCode = response.postBuyFormShipmentAddress.postBuyFormAdrPostcode,
                MessageToSeller = response.postBuyFormMsgToSeller,
            };

            return transaction;
        }

        private void ThrowIfNotLogged()
        {
            if(!IsLogged)
                throw new InvalidOperationException("Not logged in");
        }

        [Obsolete]
        public Task<doNewAuctionExtResponse> AddFakeAdd()
        {
           return _servicePort.doNewAuctionExtAsync(new doNewAuctionExtRequest
            {
                sessionHandle = _sessionKey,
                fields = new[]
                {
                    new FieldsValue
                    {
                        fid = 1,
                        fvalueString = "testow aaukcja 2"
                    },
                    new FieldsValue
                    {
                        fid = 2,
                        fvalueInt = 122252,
                        fvalueIntSpecified = true
                    },
                    new FieldsValue
                    {
                        fid = 8,
                        fvalueFloat = 35,
                        fvalueFloatSpecified = true
                    },
                    new FieldsValue
                    {
                        fid = 24,
                        fvalueString = "dsadasnw qhjoqwhouiheuioqwhjku hedujkqwhjkdhnkbawskldjwqbhnkjfhqwklr"
                    },
                    new FieldsValue
                    {
                        fid = 4,
                        fvalueInt = 2,
                        fvalueIntSpecified = true
                    },
                    new FieldsValue
                    {
                        fid = 5,
                        fvalueInt = 1000,
                        fvalueIntSpecified = true
                    },
                    new FieldsValue
                    {
                        fid = 9,
                        fvalueInt = 1,
                        fvalueIntSpecified = true
                    },
                    new FieldsValue
                    {
                        fid = 10,
                        fvalueInt = 5,
                        fvalueIntSpecified = true
                    },
                    new FieldsValue
                    {
                        fid = 11,
                        fvalueString = "Kraków"
                    },
                    new FieldsValue
                    {
                        fid = 32,
                        fvalueString = "33-300"
                    },
                    new FieldsValue
                    {
                        fid = 35,
                        fvalueInt = 2,
                        fvalueIntSpecified = true
                    }
                }
            });
        }
        [Obsolete]
        public void Buy(long id, float price)
        {
            var response = _servicePort.doBidItemAsync(new doBidItemRequest
            {
                sessionHandle = _sessionKey,
                bidBuyNow = 1,
                bidItId = id,
                bidQuantity = 1,
                bidUserPrice = price
            }).Result;
        }
    }

    public class AllegroCredentials
    {
        public string UserName { get; }
        public string Pass { get; }
        public string ApiKey { get; }
        public long JournalStart { get; }

        public AllegroCredentials(string userName, string pass, string apiKey, long journalStart)
        {
            UserName = userName;
            Pass = pass;
            ApiKey = apiKey;
            JournalStart = journalStart;
        }
    }
}