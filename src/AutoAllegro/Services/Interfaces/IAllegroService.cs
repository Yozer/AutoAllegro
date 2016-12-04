using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;

namespace AutoAllegro.Services.Interfaces
{
    public interface IAllegroService
    {
        Task Login(string userId, Func<AllegroCredentials> getAllegroCredentials);
        Task<List<NewAuction>> GetNewAuctions();
        Task<Auction> UpdateAuctionFees(Auction auction);
    }
}
