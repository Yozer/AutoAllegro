using System;
using System.Collections.Generic;
using System.Linq;
using AutoAllegro.Data;
using AutoAllegro.Helpers.Extensions;
using AutoAllegro.Models;
using AutoAllegro.Services.Interfaces;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutoAllegro.Services.AllegroProcessors
{
    public interface IAllegroTransactionProcessor : IAllegroAbstractProcessor
    {
    }
    public class AllegroTransactionProcessor : AllegroAbstractProcessor<IAllegroTransactionProcessor>, IAllegroTransactionProcessor
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        // https://mgmccarthy.wordpress.com/2016/11/07/using-hangfire-to-schedule-jobs-in-asp-net-core/
        private readonly ILogger<AllegroTransactionProcessor> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IAllegroService _allegroService;

        public AllegroTransactionProcessor(IBackgroundJobClient backgroundJob, ILogger<AllegroTransactionProcessor> logger,
            ApplicationDbContext db, IAllegroService allegroService)
            :base(backgroundJob, logger, Interval)
        {
            _logger = logger;
            _db = db;
            _allegroService = allegroService;
        }
        protected override void Execute()
        {
            var auctions = from auction in _db.Auctions
                           where auction.IsMonitored
                           group new KeyValuePair<long, int>(auction.AllegroAuctionId, auction.Id) by auction.UserId into g
                           select g;

            foreach (var userAuction in auctions)
            {
                string userId = userAuction.Key;
                var allegroCredentials = GetAllegroCredentials(_db, userId);
                long journalStart = allegroCredentials.JournalStart;
                _allegroService.Login(userId, allegroCredentials).Wait();

                ProcessJournal(ref journalStart, userAuction.ToDictionary(t => t.Key, t => t.Value));
            }
        }

        private void ProcessJournal(ref long journalStart, Dictionary<long, int> monitoredAds)
        {
            foreach (var dealsStruct in _allegroService.FetchJournal(journalStart))
            {
                int adId;
                if (!monitoredAds.TryGetValue(dealsStruct.dealItemId, out adId))
                    continue;

                // fetch buyer data
                Buyer buyer = _db.Buyers.FirstOrDefault(t => t.AllegroUserId == dealsStruct.dealBuyerId);
                if (buyer == null)
                {
                    buyer = _allegroService.FetchBuyerData(dealsStruct.dealItemId, dealsStruct.dealBuyerId);
                    _db.Buyers.Add(buyer);
                }

                Order order = null;

                if (dealsStruct.dealEventType == (int) EventType.DealCreated)
                {
                    order = new Order
                    {
                        AuctionId = adId,
                        AllegroDealId = dealsStruct.dealId,
                        Buyer = buyer,
                        OrderDate = dealsStruct.dealEventTime.ToDateTime(), // probably not true date
                        OrderStatus = OrderStatus.Created,
                        Quantity = dealsStruct.dealQuantity,
                        ShippingAddress = null
                    };

                    _db.Orders.Add(order);
                }
                else if (dealsStruct.dealEventType == (int) EventType.TransactionCreated)
                {
                    _logger.LogInformation($"Got transaction created dealId: {dealsStruct.dealId} transactionId: {dealsStruct.dealTransactionId}");
                    order = _db.Orders.Include(t => t.Transactions).First(t => t.AllegroDealId == dealsStruct.dealId);
                    if (order.Transactions.All(t => t.AllegroTransactionId != dealsStruct.dealTransactionId))
                    {
                        Transaction transaction = _allegroService.GetTransactionDetails(dealsStruct.dealTransactionId, order);
                        order.Transactions.Add(transaction);
                    }
                    else
                    {
                        // rare case, allegro can give us transaction finished before transaction created...
                        _logger.LogInformation($"Transaction already created for dealId: {dealsStruct.dealId} transactionId: {dealsStruct.dealTransactionId}");
                    }
                }
                else if (dealsStruct.dealEventType == (int) EventType.TransactionCanceled)
                {
                    Transaction transaction = _db.Transactions.Include(t => t.Order).First(t => t.AllegroTransactionId == dealsStruct.dealTransactionId);
                    transaction.TransactionStatus = TransactionStatus.Canceled;
                    order = transaction.Order;
                }
                else if (dealsStruct.dealEventType == (int) EventType.TransactionFinished)
                {
                    _logger.LogInformation($"Got transaction finished dealId: {dealsStruct.dealId} transactionId: {dealsStruct.dealTransactionId}");
                    Transaction transaction = _db.Transactions.Include(t => t.Order).FirstOrDefault(t => t.AllegroTransactionId == dealsStruct.dealTransactionId);
                    if (transaction == null)
                    {
                        _logger.LogInformation($"Transaction not found {dealsStruct.dealTransactionId} dealId: {dealsStruct.dealId}");
                        // rare case, allegro can give us transaction finished before transaction created...
                        order = _db.Orders.First(t => t.AllegroDealId == dealsStruct.dealId);
                        transaction = _allegroService.GetTransactionDetails(dealsStruct.dealTransactionId, order);
                        order.Transactions.Add(transaction);
                    }
                    else
                    {
                        order = transaction.Order;
                    }

                    if (order.AllegroRefundId != null)
                    {
                        _logger.LogInformation($"Pending refund ({order.AllegroRefundId.Value}) for dealId: {dealsStruct.dealId}. Trying to cancel");
                        if (_allegroService.CancelRefund(order.AllegroRefundId.Value).Result)
                        {
                            _logger.LogInformation($"Pending refund ({order.AllegroRefundId.Value}) for dealId: {dealsStruct.dealId}. Canceled successfully");
                            order.AllegroRefundId = null;
                        }
                        else
                        {
                            _logger.LogInformation($"Pending refund ({order.AllegroRefundId.Value}) for dealId: {dealsStruct.dealId}. Failed to cancel");
                        }
                    }

                    transaction.TransactionStatus = TransactionStatus.Finished;

                    // we could manually mark this order as paid
                    if (order.OrderStatus != OrderStatus.Done && order.OrderStatus != OrderStatus.Send)
                    {
                        order.OrderStatus = OrderStatus.Paid;
                    }
                }

                var e = new Event
                {
                    Order = order,
                    AllegroEventId = dealsStruct.dealEventId,
                    EventTime = dealsStruct.dealEventTime.ToDateTime(),
                    EventType = (EventType) dealsStruct.dealEventType,
                };
                _db.Events.Add(e);

                journalStart = dealsStruct.dealEventId;
                _db.SaveChanges();
            }
        }
    }
}
