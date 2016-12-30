using System;
using System.Collections.Generic;
using System.Linq;
using AutoAllegro.Data;
using AutoAllegro.Helpers.Extensions;
using AutoAllegro.Models;
using AutoAllegro.Services;
using AutoAllegro.Services.Interfaces;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SoaAllegroService;
using Xunit;

namespace AutoAllegro.Tests.Services
{
    public class AllegroProcessorTests : DatabaseMock
    {
        private readonly IBackgroundJobClient _scheduler;
        private readonly IAllegroProcessor _processor;
        private readonly IAllegroService _allegroService;

        public AllegroProcessorTests()
        {
            _scheduler = Substitute.For<IBackgroundJobClient>();
            _allegroService = Substitute.For<IAllegroService>();
            Services.AddTransient(t => _allegroService);
            Services.AddTransient<IBackgroundJobClient>(t => _scheduler);
            Services.AddSingleton<IAllegroProcessor, AllegroProcessor>();

            ServiceProvider = Services.BuildServiceProvider();
            _processor = ServiceProvider.GetService<IAllegroProcessor>();
        }

        [Fact]
        public void Init_ShouldScheduleTwoJobs()
        {
            // arrange
            CreateFakeData();

            // act
            _processor.Init();

            // assert
            _scheduler.Received(1).Create(Arg.Is<Job>(t => (string)t.Args[0] == UserId), Arg.Any<IState>());
            _scheduler.Received(1).Create(Arg.Is<Job>(t => (string)t.Args[0] == UserId2), Arg.Any<IState>());

        }
        [Fact]
        public void StartProcessor_ShouldThrow_AuctionNotMonitored()
        {
            // arrange
            CreateFakeData();
            Auction ad;
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                ad = database.Auctions.Single(t => t.Id == 4);
            }

            // act & assert
            var ex = Assert.Throws<InvalidOperationException>(() => _processor.StartProcessor(ad));
            Assert.Equal($"Auction monitoring is disabled for {ad.Id}", ex.Message);
        }
        [Fact]
        public void StopProcessor_ShouldThrow_AuctionMonitored()
        {
            // arrange
            CreateFakeData();
            Auction ad;
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                ad = database.Auctions.Single(t => t.Id == 1);
            }

            // act & assert
            var ex = Assert.Throws<InvalidOperationException>(() => _processor.StopProcessor(ad));
            Assert.Equal($"Auction monitoring is enabled for {ad.Id}", ex.Message);
        }

        [Fact]
        public void StartProcessor_ShouldNotThrow_AuctionAlreadyMonitored()
        {
            // arrange
            CreateFakeData();
            Auction ad;
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                ad = database.Auctions.Single(t => t.Id == 1);
            }
            _processor.StartProcessor(ad);
            _scheduler.ClearReceivedCalls();

            // act
            _processor.StartProcessor(ad);
            
            // assert
            _scheduler.DidNotReceiveWithAnyArgs().Create(null, null);
        }

        [Fact]
        public void StartProcessor_ShouldNotThrow_AuctionNotAdded()
        {
            // arrange
            CreateFakeData();
            Auction ad;
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                ad = database.Auctions.Single(t => t.Id == 4);
            }

            // act
            _processor.StopProcessor(ad);

            // assert
            _scheduler.DidNotReceiveWithAnyArgs().Create(null, null);
        }

        [Fact]
        public void StartProcessor_ThenStop_ThenStartAgain()
        {
            // arrange
            CreateFakeData();
            Auction ad;
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                ad = database.Auctions.Single(t => t.Id == 1);
            }

            // act
            _processor.StartProcessor(ad);
            using (var scope = CreateScope())
            {
                var db = GetDatabase(scope);
                ad = db.Auctions.Single(t => t.Id == 1);
                ad.IsMonitored = false;
                db.SaveChanges();
            }
            _processor.StopProcessor(ad);
            using(var scope = CreateScope())
            {
                var db = GetDatabase(scope);
                ad = db.Auctions.Single(t => t.Id == 1);
                ad.IsMonitored = true;
                db.SaveChanges();
            }
            _processor.StartProcessor(ad);

            // assert
            _scheduler.Received(2).Create(Arg.Is<Job>(t => (string)t.Args[0] == UserId), Arg.Any<IState>());
        }

        [Fact]
        public void Init_RemoveAd_AndAddItAgain()
        {
            // arrange
            CreateFakeData();
            Auction ad;
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                ad = database.Auctions.Single(t => t.Id == 3);
            }
            _processor.Init();
            _scheduler.ClearReceivedCalls();

            // act
            using(var scope = CreateScope())
            {
                var db = GetDatabase(scope);
                ad = db.Auctions.Single(t => t.Id == 3);
                ad.IsMonitored = false;
                db.SaveChanges();
            }
            _processor.StopProcessor(ad);
            using(var scope = CreateScope())
            {
                var db = GetDatabase(scope);
                ad = db.Auctions.Single(t => t.Id == 3);
                ad.IsMonitored = true;
                db.SaveChanges();
            }
            _processor.StartProcessor(ad);

            // assert
            _scheduler.Received(1).Create(Arg.Is<Job>(t => (string)t.Args[0] == UserId2), Arg.Any<IState>());
        }

        [Fact]
        public void Process_LogsToAllegro_NoJournalSet()
        {
            // arrange
            CreateFakeData();
            long userJournal;
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                userJournal = database.Users.Single(t => t.Id == UserId).AllegroJournalStart;
            }
            _allegroService.FetchJournal(userJournal).Returns(new List<SiteJournalDealsStruct>());
            _allegroService.IsLoginRequired(UserId).Returns(true);
            _allegroService.ClearReceivedCalls();

            // act
            _processor.Process(UserId, 0);

            // assert
            _allegroService.Received().Login(UserId, Arg.Any<AllegroCredentials>());
            Assert.Equal(2, _allegroService.ReceivedCalls().Count());
        }
        [Fact]
        public void Process_LogsToAllegro_JournalSet()
        {
            // arrange
            CreateFakeData();
            long userJournal;
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                userJournal = database.Users.Single(t => t.Id == UserId).AllegroJournalStart;
            }
            _allegroService.FetchJournal(userJournal).Returns(new List<SiteJournalDealsStruct>());
            _allegroService.IsLoginRequired(UserId).Returns(true);

            // act
            _processor.Process(UserId, userJournal);

            // assert
            _allegroService.Received().Login(UserId, Arg.Any<AllegroCredentials>());
            Assert.Equal(3, _allegroService.ReceivedCalls().Count());
        }

        [Fact]
        public void Process_DealCreated_GetBuyerData_AndInsertOrder()
        {
            // arrange
            CreateFakeData();
            long userJournal;
            Auction ad;
            Buyer buyer;
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                userJournal = database.Users.Single(t => t.Id == UserId).AllegroJournalStart;
                ad = database.Auctions.Single(t => t.Id == 1);
                buyer = database.Buyers.Single(t => t.Id == 1);
            }

            buyer.Orders = null;
            _allegroService.FetchJournal(userJournal).Returns(new List<SiteJournalDealsStruct>
            {
                new SiteJournalDealsStruct
                {
                    dealBuyerId = (int) buyer.AllegroUserId,
                    dealEventId = 152,
                    dealItemId = ad.AllegroAuctionId,
                    dealEventTime = new DateTime(2015, 4, 6, 5, 8, 4).FromDateTime(),
                    dealQuantity = 3,
                    dealEventType = (int)EventType.DealCreated,
                    dealId = 5
                }
            });
            _allegroService.FetchBuyerData(ad.AllegroAuctionId, buyer.AllegroUserId).Returns(buyer);
            ClearTransactionData();

            // act
            _processor.Process(UserId, userJournal);

            // assert
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                _allegroService.Received().FetchBuyerData(ad.AllegroAuctionId, buyer.AllegroUserId);
                Assert.Equal(3, _allegroService.ReceivedCalls().Count()); // login, journal, buyer data

                Order order = database.Orders.Include(t => t.Buyer).Include(t => t.Auction).Single();
                Assert.Equal(ad.AllegroAuctionId, order.Auction.AllegroAuctionId);
                Assert.Equal(5, order.AllegroDealId);
                Assert.Equal(new DateTime(2015, 4, 6, 5, 8, 4), order.OrderDate);
                Assert.Equal(OrderStatus.Created, order.OrderStatus);
                Assert.Equal(3, order.Quantity);
                Assert.Null(order.ShippingAddress);
                Assert.Equal(buyer.AllegroUserId, order.Buyer.AllegroUserId);

                Event ev = database.Events.Single();
                Assert.Equal(order.Id, ev.OrderId);
                Assert.Equal(152, ev.AllegroEventId);
                Assert.Equal(new DateTime(2015, 4, 6, 5, 8, 4), ev.EventTime);
                Assert.Equal(EventType.DealCreated, ev.EventType);

                User user = database.Users.Single(t => t.Id == UserId);
                Assert.Equal(152, user.AllegroJournalStart);
            }
        }

        private void ClearTransactionData()
        {
            using(var scope = CreateScope())
            {
                var database = GetDatabase(scope);

                foreach (var b in database.Buyers)
                {
                    database.Entry(b).State = EntityState.Deleted;
                }
                foreach (var o in database.Orders)
                {
                    database.Entry(o).State = EntityState.Deleted;
                }

                database.SaveChanges();
            }
        }

        protected override void CreateFakeData()
        {
            base.CreateFakeData();
            using(var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                database.Auctions.Single(t => t.Id == 2).IsMonitored = true;
                database.SaveChanges();
            }

        }
    }
}
