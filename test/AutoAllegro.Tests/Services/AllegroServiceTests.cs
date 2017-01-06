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
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SoaAllegroService;
using Xunit;
using Xunit.Sdk;

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
            _allegroService = new AllegroService(_memoryCache, _servicePort)
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
            Assert.False(_allegroService.IsLoginRequired("userId"));
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
            Assert.True(_allegroService.IsLoginRequired("userId"));
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
        public async Task CancelRefund_ShouldThrow_WhenNotLogged()
        {
            // arrange & act
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

        private async Task Login()
        {
            await _allegroService.Login("userId", _allegroCredentials);
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
