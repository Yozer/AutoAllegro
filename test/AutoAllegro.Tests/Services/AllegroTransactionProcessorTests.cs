using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using AutoAllegro.Data;
using AutoAllegro.Helpers.Extensions;
using AutoAllegro.Models;
using AutoAllegro.Services;
using AutoAllegro.Services.AllegroProcessors;
using AutoAllegro.Services.Interfaces;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SoaAllegroService;
using Xunit;

namespace AutoAllegro.Tests.Services
{
    public class AllegroTransactionProcessorTests : DatabaseMock
    {
        private readonly IBackgroundJobClient _scheduler;
        private readonly IAllegroTransactionProcessor _processor;
        private readonly IAllegroService _allegroService;
        private readonly IServiceScope _scope;
        private ApplicationDbContext _db;

        public AllegroTransactionProcessorTests()
        {
            _scope = CreateScope();
            _scheduler = _scope.ServiceProvider.GetService<IBackgroundJobClient>();
            _allegroService = _scope.ServiceProvider.GetService<IAllegroService>();
            _db = GetDatabase(_scope);

            _processor = new AllegroTransactionProcessor(_scheduler, Substitute.For<ILogger<AllegroTransactionProcessor>>(), _db, _allegroService);
        }
        [Fact]
        public void Process_ShouldScheduleNextJobInCaseOfException()
        {
            // arrange
            CreateFakeData();
            int i = 0;
            _allegroService.Login(null, null).ReturnsForAnyArgs(t =>
            {
                ++i;
                if (i == 1)
                    throw new CommunicationException("ex");
                else if (i == 2)
                    throw new FaultException(new FaultReason("xx"), new FaultCode("x"), "yy");
                else if (i == 3)
                    throw new TimeoutException();

                return Task.CompletedTask;
            });

            // act
            _processor.Process();
            _processor.Process();
            _processor.Process();

            // assert
            _scheduler.ReceivedWithAnyArgs(3).Create(null, null);
            _allegroService.DidNotReceiveWithAnyArgs().FetchJournal(0);
        }
        [Fact]
        public void Init_ShouldScheduleJob()
        {
            // arrange
            // act
            _processor.Init();

            // assert
            _scheduler.ReceivedWithAnyArgs(1).Create(null, null);
        }
        

        [Fact]
        public void Process_DealCreated_GetBuyerData_AndInsertOrder()
        {
            // arrange
            CreateFakeData();
            long userJournal = 14;

            Auction ad = _db.Auctions.Single(t => t.Id == 1);
            Buyer buyer = _db.Buyers.Single(t => t.Id == 1);
            ClearTransactionData();

            _db.Auctions.Single(t => t.AllegroAuctionId == 333).IsMonitored = false;
            _db.Events.Add(new Event {Order = new Order {Auction = ad, Buyer = new Buyer() }, AllegroEventId = userJournal });
            _db.SaveChanges();

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
                    dealId = 5999
                }
            });
            _allegroService.FetchBuyerData(ad.AllegroAuctionId, buyer.AllegroUserId).Returns(buyer);

            // act
            _processor.Process();

            // assert
            var subScope = _scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
            _db = subScope.ServiceProvider.GetService<ApplicationDbContext>();
            _allegroService.Received().FetchBuyerData(ad.AllegroAuctionId, buyer.AllegroUserId);
            Assert.Equal(3, _allegroService.ReceivedCalls().Count()); // login, journal, buyer data

            Order order = _db.Orders.Include(t => t.Buyer).Include(t => t.Auction).Last();
            Assert.Equal(ad.AllegroAuctionId, order.Auction.AllegroAuctionId);
            Assert.Equal(5999, order.AllegroDealId);
            Assert.Equal(new DateTime(2015, 4, 6, 5, 8, 4), order.OrderDate);
            Assert.Equal(OrderStatus.Created, order.OrderStatus);
            Assert.Equal(3, order.Quantity);
            Assert.Null(order.ShippingAddress);
            Assert.Equal(buyer.AllegroUserId, order.Buyer.AllegroUserId);

            Event ev = _db.Events.Last();
            Assert.Equal(order.Id, ev.OrderId);
            Assert.Equal(152, ev.AllegroEventId);
            Assert.Equal(new DateTime(2015, 4, 6, 5, 8, 4), ev.EventTime);
            Assert.Equal(EventType.DealCreated, ev.EventType);

            User user = _db.Users.Single(t => t.Id == UserId);
            Assert.Equal(152, _db.Events.Last(t => t.Order.Auction.UserId == ad.UserId).AllegroEventId);
        }

        [Fact]
        public void Process_TwoTransactionCreated_OneCanceled_OneFinished()
        {
            // arrange
            CreateFakeData();
            Order order = new Order
            {
                AllegroDealId = 512,
                OrderDate = new DateTime(2012, 5, 6, 4, 3, 2),
                OrderStatus = OrderStatus.Created,
                Quantity = 5
            };
            long userJournal = 22;
            Auction ad = _db.Auctions.Single(t => t.Id == 1);
            _db.Auctions.Single(t => t.AllegroAuctionId == 333).IsMonitored = false;
            Buyer buyer = _db.Buyers.Single(t => t.Id == 1);
            buyer.Orders.Add(order);
            order.Auction = ad;

            _db.Events.Add(new Event { Order = new Order { Auction = ad, Buyer = new Buyer() }, AllegroEventId = userJournal });
            _db.SaveChanges();

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
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
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

                var events = database.Events.Skip(1).ToList();
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
                Assert.Equal(155, database.Events.OrderByDescending(t => t.AllegroEventId).First(t => t.Order.Auction.UserId == user.Id).AllegroEventId);
            }
        }
        [Fact]
        public void Process_TransactionWithOpenCaseFinished_ShouldCancelRefund()
        {
            // arrange
            CreateFakeData();
            Order order = new Order
            {
                AllegroDealId = 512,
                OrderDate = new DateTime(2012, 5, 6, 4, 3, 2),
                OrderStatus = OrderStatus.Canceled,
                AllegroRefundId = 5124,
                Quantity = 5
            };
            Auction ad = _db.Auctions.Single(t => t.Id == 1);
            Buyer buyer = _db.Buyers.Single(t => t.Id == 1);
            buyer.Orders.Add(order);
            order.Auction = ad;
            _db.SaveChanges();

            _allegroService.FetchJournal(0).ReturnsForAnyArgs(new List<SiteJournalDealsStruct>
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
                    dealTransactionId = 5111,
                    dealBuyerId = (int) buyer.AllegroUserId,
                    dealEventId = 155,
                    dealItemId = ad.AllegroAuctionId,
                    dealEventTime = new DateTime(2015, 4, 6, 5, 8, 7).FromDateTime(),
                    dealEventType = (int)EventType.TransactionFinished,
                    dealId = 512
                }
            });
            _allegroService.GetTransactionDetails(Arg.Is<long>(t => t == 5111), Arg.Is<Order>(t => t.AllegroDealId == 512)).Returns(t => new Transaction
            {
                AllegroTransactionId = t.Arg<long>(),
                Amount = ad.PricePerItem * order.Quantity,
                TransactionStatus = TransactionStatus.Created
            });
            _allegroService.CancelRefund(0).ReturnsForAnyArgs(true);

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);
                var orderCheck = database.Orders.Include(t => t.ShippingAddress).Include(t => t.Transactions).Single(t => t.AllegroDealId == 512);
                Assert.Equal(OrderStatus.Paid, orderCheck.OrderStatus);
                Assert.Null(orderCheck.AllegroRefundId);
                _allegroService.Received(1).CancelRefund(5124);
            }
        }
        [Fact]
        public void Process_TransactionWithOpenCaseFinished_AllegroServiceReturnsFalse_ShouldNotCancelRefund()
        {
            // arrange
            CreateFakeData();
            Order order = new Order
            {
                AllegroDealId = 512,
                OrderDate = new DateTime(2012, 5, 6, 4, 3, 2),
                OrderStatus = OrderStatus.Canceled,
                AllegroRefundId = 5124,
                Quantity = 5
            };
            Auction ad = _db.Auctions.Single(t => t.Id == 1);
            Buyer buyer = _db.Buyers.Single(t => t.Id == 1);
            buyer.Orders.Add(order);
            order.Auction = ad;
            _db.SaveChanges();

            _allegroService.FetchJournal(0).ReturnsForAnyArgs(new List<SiteJournalDealsStruct>
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
                    dealTransactionId = 5111,
                    dealBuyerId = (int) buyer.AllegroUserId,
                    dealEventId = 155,
                    dealItemId = ad.AllegroAuctionId,
                    dealEventTime = new DateTime(2015, 4, 6, 5, 8, 7).FromDateTime(),
                    dealEventType = (int)EventType.TransactionFinished,
                    dealId = 512
                }
            });
            _allegroService.GetTransactionDetails(Arg.Is<long>(t => t == 5111), Arg.Is<Order>(t => t.AllegroDealId == 512)).Returns(t => new Transaction
            {
                AllegroTransactionId = t.Arg<long>(),
                Amount = ad.PricePerItem * order.Quantity,
                TransactionStatus = TransactionStatus.Created
            });
            _allegroService.CancelRefund(0).ReturnsForAnyArgs(false);

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);
                var orderCheck = database.Orders.Include(t => t.ShippingAddress).Include(t => t.Transactions).Single(t => t.AllegroDealId == 512);
                Assert.Equal(OrderStatus.Paid, orderCheck.OrderStatus);
                Assert.Equal(5124, orderCheck.AllegroRefundId);
                _allegroService.Received(1).CancelRefund(5124);
            }
        }
        private void ClearTransactionData()
        {
            foreach (var b in _db.Buyers)
            {
                _db.Entry(b).State = EntityState.Deleted;
            }
            foreach (var o in _db.Orders)
            {
                _db.Entry(o).State = EntityState.Deleted;
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
