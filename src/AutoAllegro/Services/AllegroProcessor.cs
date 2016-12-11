using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AutoAllegro.Data;
using AutoAllegro.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net.Sockets;
using System.ServiceModel;
using AutoAllegro.Helpers.Extensions;
using AutoAllegro.Models;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

using UsersContainer = System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<long, int>>;

namespace AutoAllegro.Services
{
    public class AllegroProcessor
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        // https://mgmccarthy.wordpress.com/2016/11/07/using-hangfire-to-schedule-jobs-in-asp-net-core/
        private readonly IBackgroundJobClient _backgroundJob;
        private readonly IServiceProvider _serviceProvider;
        private readonly UsersContainer _users = new UsersContainer();

        public AllegroProcessor(IBackgroundJobClient backgroundJob, IServiceProvider serviceProvider)
        {
            _backgroundJob = backgroundJob;
            _serviceProvider = serviceProvider;
        }

        public IServiceScope GetScope()
        {
            return _serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        }
        public void Init()
        {
            using (var scope = GetScope())
            {
                var db = scope.ServiceProvider.GetService<ApplicationDbContext>();
                var users = from user in db.Users.Include(t => t.Auctions)
                    from auction in user.Auctions
                    where auction.IsMonitored
                    select user;

                foreach (var user in users)
                {
                    foreach (var auction in user.Auctions)
                    {
                        StartProcessor(user, auction);
                    }
                }
            }
        }

        public void StartProcessor(User user, Auction auction)
        {
            var auctions = _users.GetOrAdd(user.Id, t => new ConcurrentDictionary<long, int>());
            auctions.TryAdd(auction.AllegroAuctionId, auction.Id);

            if (auctions.Count == 1)
            {
                _backgroundJob.Schedule(() => Process(user.Id, user.AllegroJournalStart), Interval);
            }
        }

        public void StopProcessor(User user, Auction auction)
        {
            var auctions = _users.GetOrAdd(user.Id, t => new ConcurrentDictionary<long, int>());
            int removed;
            auctions.TryRemove(auction.AllegroAuctionId, out removed);

            if (auctions.Count == 0)
            {
                ConcurrentDictionary<long, int> dummy;
                _users.TryRemove(user.Id, out dummy);
            }
        }

        public void Process(string userId, long journalStart)
        {
            var allegroService = _serviceProvider.GetService<IAllegroService>();
            using (var scope = GetScope())
            {
                var db = scope.ServiceProvider.GetService<ApplicationDbContext>();

                if (journalStart == 0)
                {
                    var allegroCredentials = GetAllegroCredentials(db, userId);
                    journalStart = allegroCredentials.JournalStart;
                    allegroService.Login(userId, () => allegroCredentials).Wait();
                }
                else
                {
                    allegroService.Login(userId, () => GetAllegroCredentials(db, userId)).Wait();
                }

                ProcessJournal(db, allegroService, userId, ref journalStart);
                //SendCodes();
            }

            if (_users.ContainsKey(userId))
                _backgroundJob.Schedule(() => Process(userId, journalStart), Interval);
        }

        private void ProcessJournal(ApplicationDbContext db, IAllegroService allegroService, string userId, ref long journalStart)
        {
            var auctions = (from auction in db.Auctions
                where auction.UserId == userId && auction.IsMonitored
                select auction).ToDictionary(t => t.AllegroAuctionId);

            foreach (var dealsStruct in allegroService.FetchJournal(journalStart))
            {
                Auction auction;
                if (!auctions.TryGetValue(dealsStruct.dealItemId, out auction))
                    continue;

                // fetch buyer data
                Buyer buyer = db.Buyers.FirstOrDefault(t => t.AllegroUserId == dealsStruct.dealBuyerId);
                if (buyer == null)
                    buyer = allegroService.FetchBuyerData(dealsStruct.dealItemId, dealsStruct.dealBuyerId);

                Order order = null;

                if (dealsStruct.dealEventType == (int) EventType.DealCreated)
                {
                    order = new Order
                    {
                        AuctionId = auction.Id,
                        AllegroDealId = dealsStruct.dealId,
                        Buyer = buyer,
                        OrderDate = dealsStruct.dealEventTime.ToDateTime(), // probably not true date
                        OrderStatus = OrderStatus.Created,
                        Quantity = dealsStruct.dealQuantity
                    };

                    db.Orders.Add(order);
                }
                else if (dealsStruct.dealEventType == (int) EventType.TransactionCreated)
                {
                    order = db.Orders.First(t => t.AllegroDealId == dealsStruct.dealId);
                    Transaction transaction = allegroService.GetTransactionDetalis(dealsStruct.dealTransactionId, order);
                    db.Transactions.Add(transaction);
                }
                else if (dealsStruct.dealEventType == (int) EventType.TransactionCanceled)
                {
                    Transaction transaction = db.Transactions.Include(t => t.Order).First(t => t.AllegroTransactionId == dealsStruct.dealTransactionId);
                    transaction.TransactionStatus = TransactionStatus.Canceled;
                    order = transaction.Order;
                }
                else if (dealsStruct.dealEventType == (int) EventType.TransactionFinished)
                {
                    // TODO handle case when user have open unpaid case and he paid

                    Transaction transaction = db.Transactions.Include(t => t.Order).First(t => t.AllegroTransactionId == dealsStruct.dealTransactionId);
                    transaction.TransactionStatus = TransactionStatus.Finished;
                    order = transaction.Order;
                    order.OrderStatus = OrderStatus.Paid;
                }

                var e = new Event
                {
                    Order = order,
                    AllegroEventId = dealsStruct.dealEventId,
                    EventTime = dealsStruct.dealEventTime.ToDateTime(),
                    EventType = (EventType) dealsStruct.dealEventType,
                };
                db.Events.Add(e);

                journalStart = dealsStruct.dealEventId;
            }

            db.SaveChanges();
        }

        private AllegroCredentials GetAllegroCredentials(ApplicationDbContext db, string id)
        {
            var user = db.Users.Single(t => t.Id == id);
            return new AllegroCredentials(user.AllegroUserName, user.AllegroHashedPass, user.AllegroKey, user.AllegroJournalStart);
        }
    }
}
