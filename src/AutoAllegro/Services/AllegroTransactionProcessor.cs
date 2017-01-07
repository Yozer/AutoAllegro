﻿using System;
using System.Collections.Generic;
using AutoAllegro.Data;
using AutoAllegro.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.ServiceModel;
using AutoAllegro.Models;
using Hangfire;
using AutoAllegro.Helpers.Extensions;
using Microsoft.Extensions.Logging;

namespace AutoAllegro.Services
{
    public class AllegroTransactionProcessor : IAllegroTransactionProcessor
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        // https://mgmccarthy.wordpress.com/2016/11/07/using-hangfire-to-schedule-jobs-in-asp-net-core/
        private readonly IBackgroundJobClient _backgroundJob;
        private readonly ILogger<AllegroTransactionProcessor> _logger;
        private readonly ApplicationDbContext _db;
        private readonly IAllegroService _allegroService;

        public AllegroTransactionProcessor(IBackgroundJobClient backgroundJob, ILogger<AllegroTransactionProcessor> logger,
            ApplicationDbContext db, IAllegroService allegroService)
        {
            _backgroundJob = backgroundJob;
            _logger = logger;
            _db = db;
            _allegroService = allegroService;
        }
        public void Init()
        {
            _backgroundJob.Schedule(() => Process(), Interval);
        }
        public void Process()
        {
            try
            {
                var auctions =  from auction in _db.Auctions
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
                    var user = _db.Users.First(t => t.Id == userId);
                    user.AllegroJournalStart = journalStart;
                    _db.SaveChanges();
                }
                
                //SendCodes(db, emailService, userId);
            }
            catch (TimeoutException e)
            {
                _logger.LogError(1, e, "The service operation timed out.");
            }
            catch (FaultException e)
            {
                _logger.LogError(1, e, "An unknown exception was received.");
            }
            catch (CommunicationException e)
            {
                _logger.LogError(1, e, "There was a communication problem.");
            }

            _backgroundJob.Schedule(() => Process(), Interval);
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
                    order = _db.Orders.First(t => t.AllegroDealId == dealsStruct.dealId);
                    Transaction transaction = _allegroService.GetTransactionDetails(dealsStruct.dealTransactionId, order);
                    order.Transactions.Add(transaction);
                }
                else if (dealsStruct.dealEventType == (int) EventType.TransactionCanceled)
                {
                    Transaction transaction = _db.Transactions.Include(t => t.Order).First(t => t.AllegroTransactionId == dealsStruct.dealTransactionId);
                    transaction.TransactionStatus = TransactionStatus.Canceled;
                    order = transaction.Order;
                }
                else if (dealsStruct.dealEventType == (int) EventType.TransactionFinished)
                {
                    _logger.LogInformation($"Transaction finished dealId: {dealsStruct.dealId}");
                    Transaction transaction = _db.Transactions.Include(t => t.Order).First(t => t.AllegroTransactionId == dealsStruct.dealTransactionId);
                    order = transaction.Order;

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
                    order.OrderStatus = OrderStatus.Paid;
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

        private AllegroCredentials GetAllegroCredentials(ApplicationDbContext db, string id)
        {
            var user = db.Users.Single(t => t.Id == id);
            return new AllegroCredentials(user.AllegroUserName, user.AllegroHashedPass, user.AllegroKey, user.AllegroJournalStart);
        }
    }
}