using System.Collections.Generic;
using System.Threading.Tasks;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using SoaAllegroService;

namespace AutoAllegro.Services.Interfaces
{
    public interface IAllegroService
    {
        Task<bool> Login(string userAllegroUserName, string userAllegroHashedPass, string userAllegroKey);
        Task<List<NewAuction>> GetNewAuctions();
        Task<Auction> UpdateAuctionFees(Auction auction);
    }
}
