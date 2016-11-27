using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Services.Interfaces;
using SoaAllegroService;

namespace AutoAllegro.Services
{
    public class AllegroService : IAllegroService
    {
        private const int CountryCode = 1;

        private readonly servicePort _servicePort;
        private string _sessionKey;
        public bool IsLogged => _sessionKey != null;

        public AllegroService()
        {
            _servicePort = new servicePortClient();
        }

        public Task<bool> Login(string username, string pass, string key)
        {
            if(IsLogged)
                throw new InvalidOperationException();

            return _servicePort.doQuerySysStatusAsync(new doQuerySysStatusRequest(1, CountryCode, key)).ContinueWith(sys =>
            {
                return _servicePort.doLoginEncAsync(new doLoginEncRequest
                {
                    countryCode = CountryCode,
                    webapiKey = key,
                    userHashPassword = pass,
                    userLogin = username,
                    localVersion = sys.Result.verKey,
                }).ContinueWith(login =>
                {
                    if (login.IsFaulted)
                        return false;
                    _sessionKey = login.Result.sessionHandlePart;
                    return true;
                });
            }).Unwrap();
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

    public static class AllegroServiceExtensions
    {
        public static DateTime ToDateTime(this long unixTimeStamp)
        {
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dateTime;
        }
    }
}