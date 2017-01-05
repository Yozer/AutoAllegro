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
using NSubstitute.ExceptionExtensions;
using SoaAllegroService;
using Xunit;

namespace AutoAllegro.Tests.Services
{
    public class AllegroProcessorTests : DatabaseMock
    {
        private readonly IBackgroundJobClient _scheduler;
        private readonly IAllegroProcessor _processor;
        private readonly IAllegroService _allegroService;
        private readonly IEmailSender _emailService;

        public AllegroProcessorTests()
        {
            _scheduler = Substitute.For<IBackgroundJobClient>();
            _allegroService = Substitute.For<IAllegroService>();
            _emailService = Substitute.For<IEmailSender>();
            Services.AddTransient(t => _emailService);
            Services.AddTransient(t => _allegroService);
            Services.AddTransient<IBackgroundJobClient>(t => _scheduler);
            Services.AddSingleton<IAllegroProcessor, AllegroProcessor>();

            ServiceProvider = Services.BuildServiceProvider();
            InitDatabase();
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

        [Fact]
        public void Process_TwoTransactionCreated_OneCanceled_OneFinished()
        {
            // arrange
            CreateFakeData();
            long userJournal;
            Auction ad;
            Buyer buyer;
            Order order = new Order
            {
                AllegroDealId = 512,
                OrderDate = new DateTime(2012, 5, 6, 4, 3, 2),
                OrderStatus = OrderStatus.Created,
                Quantity = 5
            };
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                userJournal = database.Users.Single(t => t.Id == UserId).AllegroJournalStart;
                ad = database.Auctions.Single(t => t.Id == 1);
                buyer = database.Buyers.Single(t => t.Id == 1);
                buyer.Orders.Add(order);
                order.Auction = ad;
                database.SaveChanges();
            }

            _allegroService.FetchJournal(userJournal).Returns(new List<SiteJournalDealsStruct>
            {
                new SiteJournalDealsStruct
                {
                    dealTransactionId = 5111,
                    dealBuyerId = (int) buyer.AllegroUserId,
                    dealEventId = 152,
                    dealItemId = ad.AllegroAuctionId,
                    dealEventTime = new DateTime(2015, 4, 6, 5, 8, 4).FromDateTime(),
                    dealEventType = (int)EventType.TransactionCreated,
                    dealId = 512
                },
                new SiteJournalDealsStruct
                {
                    dealTransactionId = 5112,
                    dealBuyerId = (int) buyer.AllegroUserId,
                    dealEventId = 153,
                    dealItemId = ad.AllegroAuctionId,
                    dealEventTime = new DateTime(2015, 4, 6, 5, 8, 5).FromDateTime(),
                    dealEventType = (int)EventType.TransactionCreated,
                    dealId = 512
                },
                new SiteJournalDealsStruct
                {
                    dealTransactionId = 5111,
                    dealBuyerId = (int) buyer.AllegroUserId,
                    dealEventId = 154,
                    dealItemId = ad.AllegroAuctionId,
                    dealEventTime = new DateTime(2015, 4, 6, 5, 8, 6).FromDateTime(),
                    dealEventType = (int)EventType.TransactionCanceled,
                    dealId = 512
                },
                new SiteJournalDealsStruct
                {
                    dealTransactionId = 5112,
                    dealBuyerId = (int) buyer.AllegroUserId,
                    dealEventId = 155,
                    dealItemId = ad.AllegroAuctionId,
                    dealEventTime = new DateTime(2015, 4, 6, 5, 8, 7).FromDateTime(),
                    dealEventType = (int)EventType.TransactionFinished,
                    dealId = 512
                }
            });
            _allegroService.GetTransactionDetails(Arg.Is<long>(t => t == 5111 || t == 5112), Arg.Is<Order>(t => t.AllegroDealId == 512)).Returns(t => new Transaction
            {
                AllegroTransactionId = t.Arg<long>(),
                Amount = ad.PricePerItem * order.Quantity,
                TransactionStatus = TransactionStatus.Created
            }).AndDoes(t =>
            {
                Order o = t.Arg<Order>();
                o.ShippingAddress = new ShippingAddress
                {
                    Address = "add",
                    City = "city",
                    FirstName = "finame",
                    LastName = "laName",
                    MessageToSeller = "msg",
                    PostCode = "33-331"
                };
            });

            // act
            _processor.Process(UserId, userJournal);

            // assert
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                _allegroService.Received(1).GetTransactionDetails(5111L, Arg.Is<Order>(t => t.AllegroDealId == 512));
                _allegroService.Received(1).GetTransactionDetails(5112L, Arg.Is<Order>(t => t.AllegroDealId == 512));
                Assert.Equal(4, _allegroService.ReceivedCalls().Count()); // login, journal, 2x transaction data

                var orderCheck = database.Orders.Include(t => t.ShippingAddress).Include(t => t.Transactions).Single(t => t.AllegroDealId == 512);
                Assert.Equal(OrderStatus.Paid, orderCheck.OrderStatus);
                Assert.Equal("add", orderCheck.ShippingAddress.Address);
                Assert.Equal("city", orderCheck.ShippingAddress.City);
                Assert.Equal("finame", orderCheck.ShippingAddress.FirstName);
                Assert.Equal("laName", orderCheck.ShippingAddress.LastName);
                Assert.Equal("msg", orderCheck.ShippingAddress.MessageToSeller);
                Assert.Equal("33-331", orderCheck.ShippingAddress.PostCode);

                Assert.Equal(2, orderCheck.Transactions.Count);
                var transaction1 = orderCheck.Transactions.Single(t => t.AllegroTransactionId == 5111);
                var transaction2 = orderCheck.Transactions.Single(t => t.AllegroTransactionId == 5112);
                Assert.Equal(TransactionStatus.Canceled, transaction1.TransactionStatus);
                Assert.Equal(TransactionStatus.Finished, transaction2.TransactionStatus);

                var events = database.Events.ToList();
                Assert.Equal(4, events.Count);
                Assert.Equal(order.Id, events[0].OrderId);
                Assert.Equal(152, events[0].AllegroEventId);
                Assert.Equal(new DateTime(2015, 4, 6, 5, 8, 4), events[0].EventTime);
                Assert.Equal(EventType.TransactionCreated, events[0].EventType);
                Assert.Equal(order.Id, events[1].OrderId);
                Assert.Equal(153, events[1].AllegroEventId);
                Assert.Equal(new DateTime(2015, 4, 6, 5, 8, 5), events[1].EventTime);
                Assert.Equal(EventType.TransactionCreated, events[1].EventType);
                Assert.Equal(order.Id, events[2].OrderId);
                Assert.Equal(154, events[2].AllegroEventId);
                Assert.Equal(new DateTime(2015, 4, 6, 5, 8, 6), events[2].EventTime);
                Assert.Equal(EventType.TransactionCanceled, events[2].EventType);
                Assert.Equal(order.Id, events[3].OrderId);
                Assert.Equal(155, events[3].AllegroEventId);
                Assert.Equal(new DateTime(2015, 4, 6, 5, 8, 7), events[3].EventTime);
                Assert.Equal(EventType.TransactionFinished, events[3].EventType);

                User user = database.Users.Single(t => t.Id == UserId);
                Assert.Equal(155, user.AllegroJournalStart);
            }
        }

        [Fact]
        public void Process_SendCodesToBuyer()
        {
            // arrange
            CreateFakeData();
            long userJournal;
            Auction ad;
            Buyer buyer = new Buyer
            {
                Email = "wp@wp.pl",
                FirstName = "firstName",
                LastName = "lastName"
            };
            Order order = new Order
            {
                AllegroDealId = 512,
                OrderDate = new DateTime(2012, 5, 6, 4, 3, 2),
                OrderStatus = OrderStatus.Paid,
                Quantity = 1
            };
            Order order2 = new Order
            {
                AllegroDealId = 513,
                OrderDate = new DateTime(2012, 5, 6, 4, 3, 2),
                OrderStatus = OrderStatus.Paid,
                Quantity = 3
            };
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                userJournal = database.Users.Single(t => t.Id == UserId).AllegroJournalStart;
                ad = database.Auctions.Include(t => t.User).Single(t => t.AllegroAuctionId == 111);
                ad.Orders.Add(order);
                ad.Orders.Add(order2);

                ad.GameCodes = new List<GameCode>
                {
                    new GameCode {Code = "xxx"},
                    new GameCode {Code = "yyy"},
                    new GameCode {Code = "zzz"},
                    new GameCode {Code = "ggg"},
                    new GameCode {Code = "fff"},
                    new GameCode {Code = "zzz"},
                };
                ad.Converter = 2;
                ad.User.VirtualItemSettings = new VirtualItemSettings
                {
                    DisplayName = "DisplayName",
                    MessageSubject= "subject",
                    MessageTemplate = "Hi {FIRST_NAME} {LAST_NAME}! You bought {QUANTITY}.<br>Your codes:<br>{ITEM}",
                    ReplyTo = "www@wp.pl"
                };

                order.Buyer = buyer;
                order2.Buyer = buyer;
                database.SaveChanges();
            }

            _allegroService.FetchJournal(userJournal).Returns(new List<SiteJournalDealsStruct>());

            // act
            _processor.Process(UserId, userJournal);

            // assert
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                Assert.Equal(2, _allegroService.ReceivedCalls().Count()); // login, journal
                _emailService.Received(1).SendEmailAsync("wp@wp.pl", "subject", "Hi firstName lastName! You bought 1.<br>Your codes:<br>xxx<br>yyy", "www@wp.pl", "DisplayName");

                order = database.Orders.Include(t => t.GameCodes).First(t => t.AllegroDealId == 512);
                order2 = database.Orders.Include(t => t.GameCodes).First(t => t.AllegroDealId == 513);
                var codes = database.GameCodes.Include(t => t.Order).ToList();

                Assert.Equal(2, order.GameCodes.Count);
                Assert.Equal("xxx", order.GameCodes.ElementAt(0).Code);
                Assert.Equal("yyy", order.GameCodes.ElementAt(1).Code);
                Assert.Equal(OrderStatus.Done, order.OrderStatus);

                Assert.Equal(0, order2.GameCodes.Count);
                Assert.Equal(OrderStatus.Paid, order2.OrderStatus);

                Assert.Equal(6, codes.Count);
                Assert.Equal(4, codes.Count(t => t.Order == null));
            }
        }
        [Fact]
        public void Process_SendCodesToBuyer_EmailSenderThrowsException()
        {
            // arrange
            CreateFakeData();
            long userJournal;
            Auction ad;
            Buyer buyer = new Buyer
            {
                Email = "wp@wp.pl",
                FirstName = "firstName",
                LastName = "lastName"
            };
            Order order = new Order
            {
                AllegroDealId = 512,
                OrderDate = new DateTime(2012, 5, 6, 4, 3, 2),
                OrderStatus = OrderStatus.Paid,
                Quantity = 1
            };
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                userJournal = database.Users.Single(t => t.Id == UserId).AllegroJournalStart;
                ad = database.Auctions.Include(t => t.User).Single(t => t.AllegroAuctionId == 111);
                ad.Orders.Add(order);

                ad.GameCodes = new List<GameCode>
                {
                    new GameCode {Code = "xxx"},
                };
                ad.User.VirtualItemSettings = new VirtualItemSettings
                {
                    MessageSubject = "subject",
                    MessageTemplate = "Hi {FIRST_NAME} {LAST_NAME}! You bought {QUANTITY}.<br>Your codes:<br>{ITEM}",
                };

                order.Buyer = buyer;
                database.SaveChanges();
            }

            _allegroService.FetchJournal(userJournal).Returns(new List<SiteJournalDealsStruct>());
            _emailService.SendEmailAsync(null, null, null).ThrowsForAnyArgs(new Exception());

            // act
            _processor.Process(UserId, userJournal);

            // assert
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                Assert.Equal(2, _allegroService.ReceivedCalls().Count()); // login, journal
                _emailService.Received(1).SendEmailAsync("wp@wp.pl", "subject", "Hi firstName lastName! You bought 1.<br>Your codes:<br>xxx");

                order = database.Orders.Include(t => t.GameCodes).First(t => t.AllegroDealId == 512);
                var codes = database.GameCodes.Include(t => t.Order).ToList();

                Assert.Equal(0, order.GameCodes.Count);
                Assert.Equal(OrderStatus.Paid, order.OrderStatus);
                Assert.Equal(1, codes.Count(t => t.Order == null));
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
