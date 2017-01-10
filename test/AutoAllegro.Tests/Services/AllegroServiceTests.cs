using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using AutoAllegro.Helpers.Extensions;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SoaAllegroService;
using Xunit;

namespace AutoAllegro.Tests.Services
{
    public class AllegroServiceTests
    {
        private const string _session = "session";
        private readonly AllegroService _allegroService;
        private readonly servicePort _servicePort;
        private readonly MemoryCache _memoryCache;

        private readonly string _apiKey;
        private readonly AllegroCredentials _allegroCredentials;
        private readonly TimeSpan _apiSessionExpiration = TimeSpan.FromSeconds(1);
        private readonly doQuerySysStatusResponse _sysResponse = new doQuerySysStatusResponse("info", 1251);

        public AllegroServiceTests()
        {
            _apiKey = "TestApiKey";
            _allegroCredentials = new AllegroCredentials("testName", "testPass", _apiKey, 41);

            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _servicePort = Substitute.For<servicePort>();
            _allegroService = new AllegroService(_memoryCache, _servicePort, Substitute.For<ILogger<AllegroService>>())
            {
                WebApiKeyExpirationTime = _apiSessionExpiration
            };

            _servicePort.doQuerySysStatusAsync(Arg.Is<doQuerySysStatusRequest>(t => t.sysvar == 1 && t.countryId == 1 && t.webapiKey == _apiKey))
                .Returns(Task.FromResult(_sysResponse));
        }
        [Fact]
        public async Task Login_ShouldLogin_WhenNoSessionIsAvailable()
        {
            // arrange
            MockLogin();

            // act
            await _allegroService.Login("userId", _allegroCredentials);

            // assert
            Assert.True(_allegroService.IsLogged);
            await _servicePort.ReceivedWithAnyArgs(1).doLoginEncAsync(null);
            await _servicePort.ReceivedWithAnyArgs(1).doQuerySysStatusAsync(null);
        }

        [Fact]
        public async Task Login_ShouldGetSessionFromCache_WhenAlreadyLogged()
        {
            // arrange
            MockLogin();

            // act
            await _allegroService.Login("userId", _allegroCredentials);
            Thread.Sleep(500);
            await _allegroService.Login("userId", null);

            // assert
            Assert.True(_allegroService.IsLogged);
            await _servicePort.ReceivedWithAnyArgs(1).doLoginEncAsync(null);
            await _servicePort.ReceivedWithAnyArgs(1).doQuerySysStatusAsync(null);
        }
        [Fact]
        public async Task Login_ShouldLogin_WhenCachedSessionHasExpired()
        {
            // arrange
            MockLogin();

            // act
            await _allegroService.Login("userId", _allegroCredentials);
            Thread.Sleep(1200);
            Assert.False(_allegroService.IsLogged);
            await _allegroService.Login("userId", _allegroCredentials);

            // assert
            Assert.True(_allegroService.IsLogged);
            await _servicePort.ReceivedWithAnyArgs(2).doLoginEncAsync(null);
            await _servicePort.ReceivedWithAnyArgs(2).doQuerySysStatusAsync(null);
        }

        [Fact]
        public async Task GetNewAuctions_ShouldReturnNewAuctions()
        {
            // arrange
            MockLogin();
            await Login();
            _servicePort.doGetMySellItemsAsync(Arg.Is<doGetMySellItemsRequest>(t => t.sessionId == _session))
                .Returns(new doGetMySellItemsResponse
                {
                    sellItemsList = new[]
                    {
                        new SellItemStruct
                        {
                            itemId = 2,
                            itemTitle = "name1",
                            itemStartTime = new DateTime(2011, 2, 3).FromDateTime(),
                            itemEndTime = new DateTime(2022, 5, 7).FromDateTime(),
                            itemPrice = new[] {new ItemPriceStruct {priceValue = 5.23f}}
                        },
                        new SellItemStruct
                        {
                            itemId = 3,
                            itemTitle = "name2",
                            itemStartTime = new DateTime(2012, 2, 3).FromDateTime(),
                            itemEndTime = new DateTime(2023, 5, 7).FromDateTime(),
                            itemPrice = new[] {new ItemPriceStruct {priceValue = 5.25f}}
                        }
                    }
                });

            // act
            List<NewAuction> result = await _allegroService.GetNewAuctions();

            // assert
            Assert.Equal(2, result.Count);
        }
        [Fact]
        public async Task SendRefund_ShouldSendRefund()
        {
            // arrange
            MockLogin();
            await Login();
            _servicePort.doSendRefundFormAsync(null).ReturnsForAnyArgs(new doSendRefundFormResponse {refundId = 55});
            // act
            int result = await _allegroService.SendRefund(new Order {AllegroDealId = 5, Quantity = 3}, 4);

            // assert
            Assert.Equal(55, result);
            await _servicePort.Received(1).doSendRefundFormAsync(Arg.Is<doSendRefundFormRequest>(t => t.dealId == 5 && t.reasonId == 4 && t.refundQuantity == 3));
        }
        [Fact]
        public async Task SendRefund_ShouldLoginAgainAndSendRefund_WhenSessionIsInvalid()
        {
            // arrange
            MockLogin();
            await Login();
            bool wasException = false;
            _servicePort.doSendRefundFormAsync(null).ReturnsForAnyArgs(t =>
            {
                if (wasException)
                    return new doSendRefundFormResponse {refundId = 55};

                wasException = true;
                throw new FaultException(new FaultReason("reason"), new FaultCode("ERR_NO_SESSION"), "xx");
            });
            // act
            int result = await _allegroService.SendRefund(new Order { AllegroDealId = 5, Quantity = 3 }, 4);

            // assert
            Assert.Equal(55, result);
            await _servicePort.Received(2).doSendRefundFormAsync(Arg.Is<doSendRefundFormRequest>(t => t.dealId == 5 && t.reasonId == 4 && t.refundQuantity == 3));
            await _servicePort.ReceivedWithAnyArgs(1).doLoginEncAsync(null);
            await _servicePort.ReceivedWithAnyArgs(1).doQuerySysStatusAsync(null);
        }
        [Fact]
        public async Task SendRefund_ShouldLoginAgainAndSendRefund_WhenSessionExpired()
        {
            // arrange
            MockLogin();
            await Login();
            bool wasException = false;
            _servicePort.doSendRefundFormAsync(null).ReturnsForAnyArgs(t =>
            {
                if (wasException)
                    return new doSendRefundFormResponse { refundId = 55 };

                wasException = true;
                throw new FaultException(new FaultReason("reason"), new FaultCode("ERR_SESSION_EXPIRED"), "xx");
            });
            // act
            int result = await _allegroService.SendRefund(new Order { AllegroDealId = 5, Quantity = 3 }, 4);

            // assert
            Assert.Equal(55, result);
            await _servicePort.Received(2).doSendRefundFormAsync(Arg.Is<doSendRefundFormRequest>(t => t.dealId == 5 && t.reasonId == 4 && t.refundQuantity == 3));
            await _servicePort.ReceivedWithAnyArgs(1).doLoginEncAsync(null);
            await _servicePort.ReceivedWithAnyArgs(1).doQuerySysStatusAsync(null);
        }
        [Fact]
        public async Task SendRefund_ShouldSendCancelRefund()
        {
            // arrange
            MockLogin();
            await Login();
            _servicePort.doCancelRefundFormAsync(null).ReturnsForAnyArgs(new doCancelRefundFormResponse { cancellationResult = true });
            // act
            bool result = await _allegroService.CancelRefund(4);

            // assert
            Assert.True(result);
            await _servicePort.Received(1).doCancelRefundFormAsync(Arg.Is<doCancelRefundFormRequest>(t => t.refundId == 4));
        }
        [Fact]
        public async Task SendRefund_AllegroFailsToCancel()
        {
            // arrange
            MockLogin();
            await Login();
            _servicePort.doCancelRefundFormAsync(null).ReturnsForAnyArgs(new doCancelRefundFormResponse { cancellationResult = false });
            // act
            bool result = await _allegroService.CancelRefund(4);

            // assert
            Assert.False(result);
            await _servicePort.Received(1).doCancelRefundFormAsync(Arg.Is<doCancelRefundFormRequest>(t => t.refundId == 4));
        }
        [Fact]
        public async Task SendRefund_AllegroFailsToCancelWithSoapException()
        {
            // arrange
            MockLogin();
            await Login();
            _servicePort.doCancelRefundFormAsync(null).ThrowsForAnyArgs(new FaultException(new FaultReason("xx"), new FaultCode("xx", "ww"), "action"));
            // act
            bool result = await _allegroService.CancelRefund(4);

            // assert
            Assert.False(result);
            await _servicePort.Received(1).doCancelRefundFormAsync(Arg.Is<doCancelRefundFormRequest>(t => t.refundId == 4));
        }
        [Fact]
        public async Task GivePositiveFeedback_GivesPositiveFeedback()
        {
            // arrange
            MockLogin();
            await Login();
            _servicePort.doFeedbackAsync(Arg.Is<doFeedbackRequest>(t => t.feItemId == 255 && t.feCommentType == "POS" &&
                                                                        t.feOp == 2 && t.feToUserId == 444 && t.feUseCommentTemplate == 1))
                .Returns(new doFeedbackResponse
                {
                    feedbackId = 51
                });

            // act
            int result = _allegroService.GivePositiveFeedback(255, 444);

            // assert
            Assert.Equal(51, result);
            Assert.Equal(1, _servicePort.ReceivedCalls().Count());
        }
        [Fact]
        public async Task FetchBuyerData_FetchsBuyerData()
        {
            // arrange
            const int buyerId = 222;
            const long adId = 441;
            MockLogin();
            await Login();
            _servicePort.doGetPostBuyDataAsync(Arg.Is<doGetPostBuyDataRequest>(t => t.buyerFilterArray[0] == buyerId && t.buyerFilterArray.Length == 1 && t.itemsArray[0] == adId && t.itemsArray.Length == 1))
                .Returns(new doGetPostBuyDataResponse
                {
                    itemsPostBuyData = new[]
                    {
                        new ItemPostBuyDataStruct
                        {
                            itemId = adId, usersPostBuyData = new[]
                            {
                                new UserPostBuyDataStruct
                                {
                                    userData = new UserDataStruct {userId = buyerId, userLogin = "login", userFirstName = "firstName", userLastName = "lastName", userPostcode = "33-300",
                                        userCity = "city", userAddress = "address", userEmail = "mail@wp.pl", userPhone = "123", userPhone2 = "2341"}
                                }
                            }
                        }
                    }
                });

            // act
            Buyer result = _allegroService.FetchBuyerData(adId, buyerId);

            // assert
            Assert.Equal(buyerId, result.AllegroUserId);
            Assert.Equal("login", result.UserLogin);
            Assert.Equal("firstName", result.FirstName);
            Assert.Equal("lastName", result.LastName);
            Assert.Equal("33-300", result.PostCode);
            Assert.Equal("city", result.City);
            Assert.Equal("address", result.Address);
            Assert.Equal("mail@wp.pl", result.Email);
            Assert.Equal("123", result.Phone);
            Assert.Equal("2341", result.Phone2);
            Assert.Equal(1, _servicePort.ReceivedCalls().Count());
        }
        [Fact]
        public async Task GetTransactionDetails_ShouldReturnTransactionAndShippingData()
        {
            // arrange
            const long transactionId = 444;
            Order order = new Order();

            MockLogin();
            await Login();
            _servicePort.doGetPostBuyFormsDataForSellersAsync(Arg.Is<doGetPostBuyFormsDataForSellersRequest>(t => t.transactionsIdsArray[0] == transactionId && t.transactionsIdsArray.Length == 1))
                .Returns(new doGetPostBuyFormsDataForSellersResponse
                {
                    postBuyFormData = new[]
                    {
                        new PostBuyFormDataStruct
                        {
                            postBuyFormPaymentAmount = 412.33f,
                            postBuyFormMsgToSeller = "seller msg",
                            postBuyFormShipmentAddress = new PostBuyFormAddressStruct
                            {
                                postBuyFormAdrStreet = "street",
                                postBuyFormAdrCity = "city",
                                postBuyFormAdrFullName = "Dominik Baran Tomasz",
                                postBuyFormAdrPostcode = "33-300"
                            }
                        }
                    }
                });

            // act
            Transaction result = _allegroService.GetTransactionDetails(transactionId, order);

            // assert
            Assert.Equal(TransactionStatus.Created, result.TransactionStatus);
            Assert.Equal(412.33m, result.Amount);
            Assert.Equal(transactionId, result.AllegroTransactionId);
            Assert.Equal("seller msg", order.ShippingAddress.MessageToSeller);
            Assert.Equal("street", order.ShippingAddress.Address);
            Assert.Equal("city", order.ShippingAddress.City);
            Assert.Equal("Dominik", order.ShippingAddress.FirstName);
            Assert.Equal("Baran Tomasz", order.ShippingAddress.LastName);
            Assert.Equal("33-300", order.ShippingAddress.PostCode);
            Assert.Equal(1, _servicePort.ReceivedCalls().Count());
        }
        [Fact]
        public async Task UpdateAuctionFees_ShouldUpdateAuctionFees()
        {
            // arrange
            Auction auction = new Auction {AllegroAuctionId = 512};

            MockLogin();
            await Login();
            _servicePort.doMyBillingItemAsync(Arg.Is<doMyBillingItemRequest>(t => t.itemId == auction.AllegroAuctionId ))
                .Returns(new doMyBillingItemResponse
                {
                    endingFees = new[]{new ItemBilling {biValue = "-21.4"}, new ItemBilling {biValue = "-44.33" } },
                    entryFees = new[]{new ItemBilling {biValue = "-442.4"}, new ItemBilling {biValue = "-0.33" } },
                });

            // act
            Auction result = await _allegroService.UpdateAuctionFees(auction);

            // assert
            Assert.Equal(65.73m, result.Fee);
            Assert.Equal(442.73m, result.OpenCost);
        }
        [Fact]
        public async Task GetWaitingFeedback_GetFeedbacks()
        {
            // arrange
            MockLogin();
            await Login();
            var items = new WaitFeedbackStruct[205];
            for (int i = 0; i < items.Length; ++i)
            {
                items[i] = new WaitFeedbackStruct
                {
                    feItemId = i * 10,
                    feToUserId = 40,
                    feAnsCommentType = "POS",
                    fePossibilityToAdd = 0,
                    feOp = 2
                };
            }
            _servicePort.doGetWaitingFeedbacksAsync(Arg.Is<doGetWaitingFeedbacksRequest>(t => t.offset == 0)).Returns(new doGetWaitingFeedbacksResponse
            {
                feWaitList = items.Take(100).ToArray()
            });
            _servicePort.doGetWaitingFeedbacksAsync(Arg.Is<doGetWaitingFeedbacksRequest>(t => t.offset == 1)).Returns(new doGetWaitingFeedbacksResponse
            {
                feWaitList = items.Skip(100).Take(100).ToArray()
            });
            _servicePort.doGetWaitingFeedbacksAsync(Arg.Is<doGetWaitingFeedbacksRequest>(t => t.offset == 2)).Returns(new doGetWaitingFeedbacksResponse
            {
                feWaitList = items.Skip(200).Take(100).ToArray()
            });

            // act
            var result = _allegroService.GetWaitingFeedback().ToList();

            // assert
            Assert.Equal(items.Length, result.Count);
            for (int i = 0; i < items.Length; ++i)
            {
                Assert.Same(items[i], result[i]);
            }
            await _servicePort.ReceivedWithAnyArgs(3).doGetWaitingFeedbacksAsync(null);
        }
        [Fact]
        public async Task FetchJournal_FetchsJournal()
        {
            // arrange
            MockLogin();
            await Login();
            var items = new SiteJournalDealsStruct[205];
            for (int i = 0; i < items.Length; ++i)
            {
                items[i] = new SiteJournalDealsStruct {dealEventId = 50 + i};
            }
            _servicePort.doGetSiteJournalDealsAsync(Arg.Is<doGetSiteJournalDealsRequest>(t => t.journalStart >= 50 && t.journalStart < 149)).Returns(new doGetSiteJournalDealsResponse
            {
                siteJournalDeals = items.Take(100).ToArray()
            });
            _servicePort.doGetSiteJournalDealsAsync(Arg.Is<doGetSiteJournalDealsRequest>(t => t.journalStart >= 149 && t.journalStart < 249)).Returns(new doGetSiteJournalDealsResponse
            {
                siteJournalDeals = items.Skip(100).Take(100).ToArray()
            });
            _servicePort.doGetSiteJournalDealsAsync(Arg.Is<doGetSiteJournalDealsRequest>(t => t.journalStart >= 249 && t.journalStart < 350)).Returns(new doGetSiteJournalDealsResponse
            {
                siteJournalDeals = items.Skip(200).Take(100).ToArray()
            });

            // act
            var result = _allegroService.FetchJournal(50).ToList();

            // assert
            Assert.Equal(items.Length, result.Count);
            for (int i = 0; i < items.Length; ++i)
            {
                Assert.Same(items[i], result[i]);
            }
            await _servicePort.ReceivedWithAnyArgs(3).doGetSiteJournalDealsAsync(null);
        }
        [Fact]
        public async Task FetchJournal_NoJournalData()
        {
            // arrange
            MockLogin();
            await Login();
            _servicePort.doGetSiteJournalDealsAsync(Arg.Is<doGetSiteJournalDealsRequest>(t => t.journalStart == 1)).Returns(new doGetSiteJournalDealsResponse
            {
                siteJournalDeals = new SiteJournalDealsStruct[0]
            });

            // act
            var result = _allegroService.FetchJournal(1).ToList();

            // assert
            Assert.Equal(0, result.Count);
            await _servicePort.ReceivedWithAnyArgs(1).doGetSiteJournalDealsAsync(null);
        }
        [Fact]
        public async Task CancelRefund_ShouldThrow_WhenNotLogged()
        {
            // arrange
            // act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _allegroService.CancelRefund(1));
            // assert
            Assert.Equal("Not logged in", exception.Message);
        }
        [Fact]
        public async Task GetNewAuctions_ShouldThrow_WhenNotLogged()
        {
            // arrange & act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _allegroService.GetNewAuctions());
            // assert
            Assert.Equal("Not logged in", exception.Message);
        }
        [Fact]
        public async Task UpdateAuctionFees_ShouldThrow_WhenNotLogged()
        {
            // arrange & act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _allegroService.UpdateAuctionFees(null));
            // assert
            Assert.Equal("Not logged in", exception.Message);
        }
        [Fact]
        public void FetchJournal_ShouldThrow_WhenNotLogged()
        {
            // arrange & act
            var exception = Assert.Throws<InvalidOperationException>(() => _allegroService.FetchJournal(0).ToList());
            // assert
            Assert.Equal("Not logged in", exception.Message);
        }
        [Fact]
        public void FetchBuyerData_ShouldThrow_WhenNotLogged()
        {
            // arrange & act
            var exception = Assert.Throws<InvalidOperationException>(() => _allegroService.FetchBuyerData(0, 0));
            // assert
            Assert.Equal("Not logged in", exception.Message);
        }
        [Fact]
        public void GetTransactionDetails_ShouldThrow_WhenNotLogged()
        {
            // arrange & act
            var exception = Assert.Throws<InvalidOperationException>(() => _allegroService.GetTransactionDetails(0, null));
            // assert
            Assert.Equal("Not logged in", exception.Message);
        }
        [Fact]
        public async Task SendRefund_ShouldThrow_WhenNotLogged()
        {
            // arrange & act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _allegroService.SendRefund(null, 0));
            // assert
            Assert.Equal("Not logged in", exception.Message);
        }
        [Fact]
        public void GetWaitingFeedback_ShouldThrow_WhenNotLogged()
        {
            // arrange & act
            var exception = Assert.Throws<InvalidOperationException>(() => _allegroService.GetWaitingFeedback().ToList());
            // assert
            Assert.Equal("Not logged in", exception.Message);
        }
        [Fact]
        public void GivePositiveFeedback_ShouldThrow_WhenNotLogged()
        {
            // arrange & act
            var exception = Assert.Throws<InvalidOperationException>(() => _allegroService.GivePositiveFeedback(0, 0));
            // assert
            Assert.Equal("Not logged in", exception.Message);
        }
        private async Task Login()
        {
            await _allegroService.Login("userId", _allegroCredentials);
            _servicePort.ClearReceivedCalls();
        }
        private void MockLogin()
        {
            _servicePort.doLoginEncAsync(Arg.Is<doLoginEncRequest>(t => VerifyLoginRequest(t))).Returns(Task.FromResult(new doLoginEncResponse
            {
                sessionHandlePart = _session
            }));
        }
        private bool VerifyLoginRequest(doLoginEncRequest request)
        {
            return request.countryCode == 1 && request.webapiKey == _apiKey && request.localVersion == _sysResponse.verKey
                   && request.userHashPassword == _allegroCredentials.Pass && request.userLogin == _allegroCredentials.UserName;
        }
    }
}
