using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using SoaAllegroService;

namespace AutoAllegro.Services.Interfaces
{
    public interface IAllegroService
    {
        Task Login(string userId, AllegroCredentials getAllegroCredentials);
        Task<List<NewAuction>> GetNewAuctions();
        Task<Auction> UpdateAuctionFees(Auction auction);
        IEnumerable<SiteJournalDealsStruct> FetchJournal(long journalStart);
        Buyer FetchBuyerData(long dealItemId, long dealBuyerId);
        Transaction GetTransactionDetails(long dealTransactionId, Order order);
        Task<int> SendRefund(Order order, int reasonId);
        Task<bool> CancelRefund(int refundId);
        IEnumerable<WaitFeedbackStruct> GetWaitingFeedback();
        int GivePositiveFeedback(long adId, int userId);
    }
}
