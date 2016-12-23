using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoAllegro.Services;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using SoaAllegroService;
using Xunit;
using Xunit.Sdk;

namespace AutoAllegro.Tests.Services
{
    public class AllegroServiceTests
    {
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
            _servicePort.doLoginEncAsync(Arg.Is<doLoginEncRequest>(t => VerifyLoginRequest(t))).Returns(Task.FromResult(new doLoginEncResponse
            {
                sessionHandlePart = "session"
            }));

            // act
            await _allegroService.Login("userId", () => _allegroCredentials);

            // assert
            Assert.True(_allegroService.IsLogged);
            await _servicePort.ReceivedWithAnyArgs(1).doLoginEncAsync(null);
            await _servicePort.ReceivedWithAnyArgs(1).doQuerySysStatusAsync(null);
        }
        [Fact]
        public async Task Login_ShouldGetSessionFromCache_WhenAlreadyLogged()
        {
            // arrange
            _servicePort.doLoginEncAsync(Arg.Is<doLoginEncRequest>(t => VerifyLoginRequest(t))).Returns(Task.FromResult(new doLoginEncResponse
            {
                sessionHandlePart = "session"
            }));

            // act
            await _allegroService.Login("userId", () => _allegroCredentials);
            Thread.Sleep(500);
            await _allegroService.Login("userId", () => { throw new EmptyException(); });

            // assert
            Assert.True(_allegroService.IsLogged);
            await _servicePort.ReceivedWithAnyArgs(1).doLoginEncAsync(null);
            await _servicePort.ReceivedWithAnyArgs(1).doQuerySysStatusAsync(null);
        }
        [Fact]
        public async Task Login_ShouldLogin_WhenCachedSessionHasExpired()
        {
            // arrange
            _servicePort.doLoginEncAsync(Arg.Is<doLoginEncRequest>(t => VerifyLoginRequest(t))).Returns(Task.FromResult(new doLoginEncResponse
            {
                sessionHandlePart = "session"
            }));

            // act
            await _allegroService.Login("userId", () => _allegroCredentials);
            Thread.Sleep(1200);
            await _allegroService.Login("userId", () => _allegroCredentials);

            // assert
            Assert.True(_allegroService.IsLogged);
            await _servicePort.ReceivedWithAnyArgs(2).doLoginEncAsync(null);
            await _servicePort.ReceivedWithAnyArgs(2).doQuerySysStatusAsync(null);
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
        private bool VerifyLoginRequest(doLoginEncRequest request)
        {
            return request.countryCode == 1 && request.webapiKey == _apiKey && request.localVersion == _sysResponse.verKey
                   && request.userHashPassword == _allegroCredentials.Pass && request.userLogin == _allegroCredentials.UserName;
        }
    }
}
