using System.Threading.Tasks;
using SoaAllegroService;

namespace AutoAllegro.Services.Interfaces
{
    public interface IAllegroService
    {
        Task<doGetCountriesResponse> GetCountries(doGetCountriesRequest request);
        Task<bool> Login(string userAllegroUserName, string userAllegroHashedPass, string userAllegroKey);
    }
}
