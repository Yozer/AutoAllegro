using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AutoAllegro.Data;
using AutoAllegro.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.ServiceModel;
using AutoAllegro.Models;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using AutoAllegro.Helpers.Extensions;

using UsersContainer = System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Generic.Dictionary<long, int>>;

namespace AutoAllegro.Services
{
    public class AllegroProcessor : IAllegroProcessor
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

        private IServiceScope GetScope()
        {
            return _serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        }
        public void Init()
        {
            using (var scope = GetScope())
            {
                var db = scope.ServiceProvider.GetService<ApplicationDbContext>();
                var auctions = from auction in db.Auctions
                            where auction.IsMonitored
                            select auction;

                foreach (var auction in auctions)
                {
                    StartProcessor(auction);
                }
            }
        }

        public void StartProcessor(Auction auction)
        {
            if(!auction.IsMonitored)
                throw new InvalidOperationException($"Auction monitoring is disabled for {auction.Id}");

            var auctions = _users.GetOrAdd(auction.UserId, t => new Dictionary<long, int>());

            bool shouldStartNewJob = auctions.Count == 0;
            auctions[auction.AllegroAuctionId] = auction.Id;

            if (shouldStartNewJob)
            {
                _backgroundJob.Schedule(() => Process(auction.UserId, 0), Interval);
            }
        }

        public void StopProcessor(Auction auction)
        {
            if (auction.IsMonitored)
                throw new InvalidOperationException($"Auction monitoring is enabled for {auction.Id}");

            var auctions = _users.GetOrAdd(auction.UserId, t => new Dictionary<long, int>());
            auctions.Remove(auction.AllegroAuctionId);

            if (auctions.Count == 0)
            {
                Dictionary<long, int> dummy;
                _users.TryRemove(auction.UserId, out dummy);
            }
        }

        public void Process(string userId, long journalStart)
        {
            var allegroService = _serviceProvider.GetService<IAllegroService>();

            using (var scope = GetScope())
            {
                try
                {
                    var db = scope.ServiceProvider.GetService<ApplicationDbContext>();


                    if (journalStart == 0)
                    {
                        var allegroCredentials = GetAllegroCredentials(db, userId);
                        journalStart = allegroCredentials.JournalStart;
                        allegroService.Login(userId, allegroCredentials).Wait();
                    }
                    else
                    {
                        if (allegroService.IsLoginRequired(userId))
                        {
                            var allegroCredentials = GetAllegroCredentials(db, userId);
                            allegroService.Login(userId, allegroCredentials).Wait();
                        }
                    }

                    ProcessJournal(db, allegroService, userId, ref journalStart);
                    //SendCodes();
                }
                catch (TimeoutException timeProblem)
                {
                    //Console.WriteLine("The service operation timed out. " + timeProblem.Message);
                }
                catch (FaultException unknownFault)
                {
                    //Console.WriteLine("An unknown exception was received. " + unknownFault.Message);
                }
                catch (CommunicationException commProblem)
                {
                    //Console.WriteLine("There was a communication problem. " + commProblem.Message + commProblem.StackTrace);
                }
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
                {
                    buyer = allegroService.FetchBuyerData(dealsStruct.dealItemId, dealsStruct.dealBuyerId);
                    db.Buyers.Add(buyer);
                }

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
                        Quantity = dealsStruct.dealQuantity,
                        ShippingAddress = null
                    };

                    db.Orders.Add(order);
                }
                else if (dealsStruct.dealEventType == (int) EventType.TransactionCreated)
                {
                    order = db.Orders.First(t => t.AllegroDealId == dealsStruct.dealId);
                    Transaction transaction = allegroService.GetTransactionDetails(dealsStruct.dealTransactionId, order);
                    order.Transactions.Add(transaction);
                    db.SaveChanges();
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

                //var user = new User { Id = userId, AllegroJournalStart = journalStart };
                //db.Users.Attach(user);
                //db.Entry(user).Property(x => x.AllegroJournalStart).IsModified = true;
                var user = db.Users.First(t => t.Id == userId);
                user.AllegroJournalStart = journalStart;
                db.SaveChanges();
            }
        }

        private AllegroCredentials GetAllegroCredentials(ApplicationDbContext db, string id)
        {
            var user = db.Users.Single(t => t.Id == id);
            return new AllegroCredentials(user.AllegroUserName, user.AllegroHashedPass, user.AllegroKey, user.AllegroJournalStart);
        }
    }
}
