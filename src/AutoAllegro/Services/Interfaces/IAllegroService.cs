using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using SoaAllegroService;

namespace AutoAllegro.Services.Interfaces
{
    public interface IAllegroService
    {
        Task Login(string userId, Func<AllegroCredentials> getAllegroCredentials);
        Task<List<NewAuction>> GetNewAuctions();
        Task<Auction> UpdateAuctionFees(Auction auction);
        IEnumerable<SiteJournalDealsStruct> FetchJournal(long journalStart);
        Buyer FetchBuyerData(long dealItemId, long dealBuyerId);
        Transaction GetTransactionDetalis(long dealTransactionId, Order order);
    }
}
