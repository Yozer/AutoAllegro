using System;
using System.Collections.Immutable;
using System.Linq;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Services.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace AutoAllegro.Services.AllegroProcessors
{
    public interface IAllegroFeedbackProcessor : IAllegroAbstractProcessor
    {
    }

    public class AllegroFeedbackProcessor : AllegroAbstractProcessor, IAllegroFeedbackProcessor
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly IBackgroundJobClient _backgroundJob;
        private readonly IAllegroService _allegroService;
        private readonly ILogger<AllegroFeedbackProcessor> _logger;
        private readonly ApplicationDbContext _db;

        public AllegroFeedbackProcessor(IBackgroundJobClient backgroundJob, IAllegroService allegroService, ILogger<AllegroFeedbackProcessor> logger, ApplicationDbContext db)
            :base(backgroundJob, logger, Interval)
        {
            _backgroundJob = backgroundJob;
            _allegroService = allegroService;
            _logger = logger;
            _db = db;
        }
        protected override void Execute()
        {
            _logger.LogInformation("Starting feedback processor");
            var auctions = from ad in _db.Auctions
                            where ad.AutomaticFeedbackEnabled
                            group new {ad.AllegroAuctionId, ad.Id} by ad.UserId into g
                            select g;

            foreach (var item in auctions)
            {
                string userId = item.Key;
                _logger.LogInformation($"Processing feedbacks for user: {userId}");

                if (_allegroService.IsLoginRequired(userId))
                {
                    var allegroCredentials = GetAllegroCredentials(_db, userId);
                    _allegroService.Login(userId, allegroCredentials).Wait();
                }

                var enabledAuctions = item.ToImmutableDictionary(t => t.AllegroAuctionId, t => t.Id);
                foreach (var feedbackStruct in _allegroService.GetWaitingFeedback())
                {
                    if (!enabledAuctions.ContainsKey(feedbackStruct.feItemId) || feedbackStruct.feOp == 1 || 
                        feedbackStruct.feAnsCommentType != "POS" || feedbackStruct.fePossibilityToAdd == 0)
                        continue;

                    int adId = enabledAuctions[feedbackStruct.feItemId];
                    var buyer = (from b in _db.Buyers
                                where b.AllegroUserId == feedbackStruct.feToUserId
                                select new
                                {
                                    Buyer = b,
                                    AllOrdersAreDone = b.Orders.Where(t => t.AuctionId == adId).All(t => t.OrderStatus == OrderStatus.Done)
                                }).Single();

                    if (buyer.AllOrdersAreDone)
                    {
                        _logger.LogInformation($"Sending request with feedback for buyer {buyer.Buyer.Id} and auction {adId}");
                        int feedbackId = _allegroService.GivePositiveFeedback(feedbackStruct.feItemId, feedbackStruct.feToUserId);

                        buyer.Buyer.ReceivedFeedbackInAuctions.Add(new AuctionBuyerFeedback
                        {
                            AuctionId = adId,
                            BuyerId = buyer.Buyer.Id,
                            AllegroFeedbackId = feedbackId
                        });
                        _db.SaveChanges();
                    }
                    else
                    {
                        _logger.LogInformation($"Cannot give feedback for buyer {buyer.Buyer.Id} and auction {feedbackStruct.feItemId}. Not all orders are done.");
                    }
                }
            }
        }
    }
}
