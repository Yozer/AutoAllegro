using System.Threading.Tasks;
using AutoAllegro.Services.Interfaces;
using SoaAllegroService;

namespace AutoAllegro.Services
{
    public class AllegroService : IAllegroService
    {
        private const int CountryCode = 1;

        private readonly servicePort _servicePort;
        private string _sessionKey;
        public AllegroService()
        {
            _servicePort = new servicePortClient();
        }

        public Task<doLoginEncResponse> Login(doLoginEncRequest request)
        {
            return _servicePort.doLoginEncAsync(request);
        }

        public Task<doGetCountriesResponse> GetCountries(doGetCountriesRequest request)
        {
            return _servicePort.doGetCountriesAsync(request);
        }

        public Task<bool> Login(string username, string pass, string key)
        {
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
    }
}