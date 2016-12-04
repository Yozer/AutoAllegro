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
        private static readonly TimeSpan WebApiKeyExpirationTime = TimeSpan.FromMinutes(55);

        private readonly IMemoryCache _memoryCache;
        private readonly servicePort _servicePort;

        private string _sessionKey;
        public bool IsLogged => _sessionKey != null;

        public AllegroService(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
            _servicePort = new servicePortClient();
        }

        public async Task Login(string userId, Func<AllegroCredentials> getAllegroCredentials)
        {
            if(IsLogged)
                throw new InvalidOperationException();

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
        public Task<List<NewAuction>> GetNewAuctions()
        {
            ThrowIfNotLogged();

            return _servicePort.doGetMySellItemsAsync(new doGetMySellItemsRequest
            {
                pageSize = 1000,
                sessionId = _sessionKey
            }).ContinueWith(task =>
            {
                return task.Result.sellItemsList.Select(t => new NewAuction
                {
                    Id = t.itemId,
                    Name = t.itemTitle,
                    StartDate = t.itemStartTime.ToDateTime(),
                    EndDate = t.itemEndTime.ToDateTime(),
                    Price = Convert.ToDecimal(t.itemPrice[0].priceValue)
                }).ToList();
            });
        }

        public Task<Auction> UpdateAuctionFees(Auction auction)
        {
            ThrowIfNotLogged();

            return _servicePort.doMyBillingItemAsync(new doMyBillingItemRequest
            {
                itemId = auction.AllegroAuctionId,
                sessionHandle = _sessionKey
            }).ContinueWith(task =>
            {
                auction.Fee = task.Result.endingFees.Sum(t => -decimal.Parse(t.biValue));
                auction.OpenCost = task.Result.entryFees.Sum(t => -decimal.Parse(t.biValue));
                return auction;
            });
        }

        private void ThrowIfNotLogged()
        {
            if(!IsLogged)
                throw new InvalidOperationException("Not logged in");
        }

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
                        fvalueString = "testow aaukcja"
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
    }

    public class AllegroCredentials
    {
        public string UserName { get; }
        public string Pass { get; }
        public string ApiKey { get; }

        public AllegroCredentials(string userName, string pass, string apiKey)
        {
            UserName = userName;
            Pass = pass;
            ApiKey = apiKey;
        }
    }
}