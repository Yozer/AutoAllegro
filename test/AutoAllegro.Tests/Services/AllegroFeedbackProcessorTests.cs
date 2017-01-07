using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using AutoAllegro.Data;
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
    public class AllegroFeedbackProcessorTests : DatabaseMock
    {
        private readonly IBackgroundJobClient _scheduler;
        private readonly IAllegroFeedbackProcessor _processor;
        private readonly IAllegroService _allegroService;
        private readonly IServiceScope _scope;
        private ApplicationDbContext _db;

        public AllegroFeedbackProcessorTests()
        {
            _scope = CreateScope();
            _scheduler = _scope.ServiceProvider.GetService<IBackgroundJobClient>();
            _allegroService = _scope.ServiceProvider.GetService<IAllegroService>();
            _db = GetDatabase(_scope);

            _processor = new AllegroFeedbackProcessor(_scheduler, _allegroService, Substitute.For<ILogger<AllegroFeedbackProcessor>>(), _db);
        }

        [Fact]
        public void Process_ShouldProcessOnlyEnabledAuctions()
        {
            // arrange
            CreateFakeData();

            _allegroService.GetWaitingFeedback().Returns(new List<WaitFeedbackStruct>
            {
                new WaitFeedbackStruct {feItemId = 111, feToUserId = 412, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 1},
                new WaitFeedbackStruct {feItemId = 247, feToUserId = 412, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 1}
            });
            _allegroService.GivePositiveFeedback(111, 412).Returns(100);

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);

                var ad1 = database.Auctions.Include(t => t.GivenFeedbackToBuyers).Single(t => t.AllegroAuctionId == 111);
                var ad2 = database.Auctions.Include(t => t.GivenFeedbackToBuyers).Single(t => t.AllegroAuctionId == 247);
                var buyer = database.Buyers.Include(t => t.ReceivedFeedbackInAuctions).Single(t => t.AllegroUserId == 412);

                Assert.Equal(1, ad1.GivenFeedbackToBuyers.Count);
                Assert.Equal(0, ad2.GivenFeedbackToBuyers.Count);
                Assert.Equal(ad1.Id, ad1.GivenFeedbackToBuyers.ElementAt(0).AuctionId);
                Assert.Equal(100, ad1.GivenFeedbackToBuyers.ElementAt(0).AllegroFeedbackId);
                Assert.Equal(buyer.Id, ad1.GivenFeedbackToBuyers.ElementAt(0).BuyerId);

                Assert.Equal(1, buyer.ReceivedFeedbackInAuctions.Count);
                Assert.Equal(ad1.Id, buyer.ReceivedFeedbackInAuctions.ElementAt(0).AuctionId);
                Assert.Equal(100, buyer.ReceivedFeedbackInAuctions.ElementAt(0).AllegroFeedbackId);
                Assert.Equal(buyer.Id, buyer.ReceivedFeedbackInAuctions.ElementAt(0).BuyerId);

                _allegroService.ReceivedWithAnyArgs(1).GivePositiveFeedback(0, 0);
                _scheduler.ReceivedWithAnyArgs(1).Create(null, null);
            }
        }
        [Fact]
        public void Process_ShouldGiveTwoFeedbacks_OneBuyerTwoAds()
        {
            // arrange
            CreateFakeData();
            _db.Auctions.Single(t => t.AllegroAuctionId == 7731).AutomaticFeedbackEnabled = true;
            var order = new Order
            {
                Buyer = _db.Buyers.Single(t => t.AllegroUserId == 412),
                Auction = _db.Auctions.Single(t => t.AllegroAuctionId == 7731),
                OrderStatus = OrderStatus.Done
            };
            _db.Orders.Add(order);
            _db.SaveChanges();

            _allegroService.GetWaitingFeedback().Returns(new List<WaitFeedbackStruct>
            {
                new WaitFeedbackStruct {feItemId = 111, feToUserId = 412, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 1},
                new WaitFeedbackStruct {feItemId = 247, feToUserId = 412, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 1},
                new WaitFeedbackStruct {feItemId = 7731, feToUserId = 412, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 1}
            });
            _allegroService.GivePositiveFeedback(111, 412).Returns(100);
            _allegroService.GivePositiveFeedback(7731, 412).Returns(1114);

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);

                var ad1 = database.Auctions.Include(t => t.GivenFeedbackToBuyers).Single(t => t.AllegroAuctionId == 111);
                var ad2 = database.Auctions.Include(t => t.GivenFeedbackToBuyers).Single(t => t.AllegroAuctionId == 247);
                var ad3 = database.Auctions.Include(t => t.GivenFeedbackToBuyers).Single(t => t.AllegroAuctionId == 7731);
                var buyer = database.Buyers.Include(t => t.ReceivedFeedbackInAuctions).Single(t => t.AllegroUserId == 412);

                Assert.Equal(1, ad1.GivenFeedbackToBuyers.Count);
                Assert.Equal(0, ad2.GivenFeedbackToBuyers.Count);
                Assert.Equal(1, ad3.GivenFeedbackToBuyers.Count);

                Assert.Equal(ad1.Id, ad1.GivenFeedbackToBuyers.ElementAt(0).AuctionId);
                Assert.Equal(100, ad1.GivenFeedbackToBuyers.ElementAt(0).AllegroFeedbackId);
                Assert.Equal(buyer.Id, ad1.GivenFeedbackToBuyers.ElementAt(0).BuyerId);

                Assert.Equal(ad3.Id, ad3.GivenFeedbackToBuyers.ElementAt(0).AuctionId);
                Assert.Equal(1114, ad3.GivenFeedbackToBuyers.ElementAt(0).AllegroFeedbackId);
                Assert.Equal(buyer.Id, ad3.GivenFeedbackToBuyers.ElementAt(0).BuyerId);

                Assert.Equal(2, buyer.ReceivedFeedbackInAuctions.Count);

                Assert.Equal(ad1.Id, buyer.ReceivedFeedbackInAuctions.ElementAt(0).AuctionId);
                Assert.Equal(100, buyer.ReceivedFeedbackInAuctions.ElementAt(0).AllegroFeedbackId);
                Assert.Equal(buyer.Id, buyer.ReceivedFeedbackInAuctions.ElementAt(0).BuyerId);

                Assert.Equal(ad3.Id, buyer.ReceivedFeedbackInAuctions.ElementAt(1).AuctionId);
                Assert.Equal(1114, buyer.ReceivedFeedbackInAuctions.ElementAt(1).AllegroFeedbackId);
                Assert.Equal(buyer.Id, buyer.ReceivedFeedbackInAuctions.ElementAt(1).BuyerId);

                _allegroService.ReceivedWithAnyArgs(2).GivePositiveFeedback(0, 0);
                _scheduler.ReceivedWithAnyArgs(1).Create(null, null);
            }
        }
        [Fact]
        public void Process_ShouldGiveTwoFeedbacks_TwoBuyersOneAd()
        {
            // arrange
            CreateFakeData();
            var buyer2 = new Buyer
            {
                AllegroUserId = 999,
            };
            var order = new Order
            {
                Buyer = buyer2,
                Auction = _db.Auctions.Single(t => t.AllegroAuctionId == 111),
                OrderStatus = OrderStatus.Done
            };
            _db.Orders.Add(order);
            _db.SaveChanges();

            _allegroService.GetWaitingFeedback().Returns(new List<WaitFeedbackStruct>
            {
                new WaitFeedbackStruct {feItemId = 111, feToUserId = 412, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 1},
                new WaitFeedbackStruct {feItemId = 247, feToUserId = 412, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 1},
                new WaitFeedbackStruct {feItemId = 111, feToUserId = 999, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 1}
            });
            _allegroService.GivePositiveFeedback(111, 412).Returns(100);
            _allegroService.GivePositiveFeedback(111, 999).Returns(51);

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);

                var ad1 = database.Auctions.Include(t => t.GivenFeedbackToBuyers).Single(t => t.AllegroAuctionId == 111);
                var ad2 = database.Auctions.Include(t => t.GivenFeedbackToBuyers).Single(t => t.AllegroAuctionId == 247);
                var buyer = database.Buyers.Include(t => t.ReceivedFeedbackInAuctions).Single(t => t.AllegroUserId == 412);
                buyer2 = database.Buyers.Include(t => t.ReceivedFeedbackInAuctions).Single(t => t.AllegroUserId == 999);

                Assert.Equal(2, ad1.GivenFeedbackToBuyers.Count);
                Assert.Equal(0, ad2.GivenFeedbackToBuyers.Count);

                Assert.Equal(ad1.Id, ad1.GivenFeedbackToBuyers.ElementAt(0).AuctionId);
                Assert.Equal(100, ad1.GivenFeedbackToBuyers.ElementAt(0).AllegroFeedbackId);
                Assert.Equal(buyer.Id, ad1.GivenFeedbackToBuyers.ElementAt(0).BuyerId);

                Assert.Equal(ad1.Id, ad1.GivenFeedbackToBuyers.ElementAt(1).AuctionId);
                Assert.Equal(51, ad1.GivenFeedbackToBuyers.ElementAt(1).AllegroFeedbackId);
                Assert.Equal(buyer2.Id, ad1.GivenFeedbackToBuyers.ElementAt(1).BuyerId);

                Assert.Equal(1, buyer.ReceivedFeedbackInAuctions.Count);

                Assert.Equal(ad1.Id, buyer.ReceivedFeedbackInAuctions.ElementAt(0).AuctionId);
                Assert.Equal(100, buyer.ReceivedFeedbackInAuctions.ElementAt(0).AllegroFeedbackId);
                Assert.Equal(buyer.Id, buyer.ReceivedFeedbackInAuctions.ElementAt(0).BuyerId);

                Assert.Equal(ad1.Id, buyer2.ReceivedFeedbackInAuctions.ElementAt(0).AuctionId);
                Assert.Equal(51, buyer2.ReceivedFeedbackInAuctions.ElementAt(0).AllegroFeedbackId);
                Assert.Equal(buyer2.Id, buyer2.ReceivedFeedbackInAuctions.ElementAt(0).BuyerId);

                _allegroService.ReceivedWithAnyArgs(2).GivePositiveFeedback(0, 0);
                _scheduler.ReceivedWithAnyArgs(1).Create(null, null);
            }
        }
        [Fact]
        public void Process_ShouldNotGiveFeedback_WeReceivedNegativeFeedback()
        {
            // arrange
            CreateFakeData();

            _allegroService.GetWaitingFeedback().Returns(new List<WaitFeedbackStruct>
            {
                new WaitFeedbackStruct {feItemId = 111, feToUserId = 412, feOp = 2, feAnsCommentType = "NEG", fePossibilityToAdd = 1}
            });

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);

                var ad1 = database.Auctions.Include(t => t.GivenFeedbackToBuyers).Single(t => t.AllegroAuctionId == 111);
                var buyer = database.Buyers.Include(t => t.ReceivedFeedbackInAuctions).Single(t => t.AllegroUserId == 412);

                Assert.Equal(0, ad1.GivenFeedbackToBuyers.Count);
                Assert.Equal(0, buyer.ReceivedFeedbackInAuctions.Count);

                _allegroService.DidNotReceiveWithAnyArgs().GivePositiveFeedback(0, 0);
                _scheduler.ReceivedWithAnyArgs(1).Create(null, null);
            }
        }
        [Fact]
        public void Process_ShouldNotGiveFeedback_ThereIsNoPossibilityToGiveFeedback()
        {
            // arrange
            CreateFakeData();

            _allegroService.GetWaitingFeedback().Returns(new List<WaitFeedbackStruct>
            {
                new WaitFeedbackStruct {feItemId = 111, feToUserId = 412, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 0}
            });

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);

                var ad1 = database.Auctions.Include(t => t.GivenFeedbackToBuyers).Single(t => t.AllegroAuctionId == 111);
                var buyer = database.Buyers.Include(t => t.ReceivedFeedbackInAuctions).Single(t => t.AllegroUserId == 412);

                Assert.Equal(0, ad1.GivenFeedbackToBuyers.Count);
                Assert.Equal(0, buyer.ReceivedFeedbackInAuctions.Count);

                _allegroService.DidNotReceiveWithAnyArgs().GivePositiveFeedback(0, 0);
                _scheduler.ReceivedWithAnyArgs(1).Create(null, null);
            }
        }
        [Fact]
        public void Process_ShouldNotGiveFeedback_GiveFeedbackOnlyToBuyers()
        {
            // arrange
            CreateFakeData();

            _allegroService.GetWaitingFeedback().Returns(new List<WaitFeedbackStruct>
            {
                new WaitFeedbackStruct {feItemId = 111, feToUserId = 412, feOp = 1, feAnsCommentType = "POS", fePossibilityToAdd = 1}
            });

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);

                var ad1 = database.Auctions.Include(t => t.GivenFeedbackToBuyers).Single(t => t.AllegroAuctionId == 111);
                var buyer = database.Buyers.Include(t => t.ReceivedFeedbackInAuctions).Single(t => t.AllegroUserId == 412);

                Assert.Equal(0, ad1.GivenFeedbackToBuyers.Count);
                Assert.Equal(0, buyer.ReceivedFeedbackInAuctions.Count);

                _allegroService.DidNotReceiveWithAnyArgs().GivePositiveFeedback(0, 0);
                _scheduler.ReceivedWithAnyArgs(1).Create(null, null);
            }
        }
        [Fact]
        public void Process_ShouldNotGiveFeedback_GiveFeedbackOnlyToFinishedOrders()
        {
            // arrange
            CreateFakeData();
            var order = new Order
            {
                Buyer = _db.Buyers.Single(t => t.AllegroUserId == 412),
                Auction = _db.Auctions.Single(t => t.AllegroAuctionId == 111),
                OrderStatus = OrderStatus.Created
            };
            _db.Orders.Add(order);
            _db.SaveChanges();

            _allegroService.GetWaitingFeedback().Returns(new List<WaitFeedbackStruct>
            {
                new WaitFeedbackStruct {feItemId = 111, feToUserId = 412, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 1}
            });

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);

                var ad1 = database.Auctions.Include(t => t.GivenFeedbackToBuyers).Single(t => t.AllegroAuctionId == 111);
                var buyer = database.Buyers.Include(t => t.ReceivedFeedbackInAuctions).Single(t => t.AllegroUserId == 412);

                Assert.Equal(0, ad1.GivenFeedbackToBuyers.Count);
                Assert.Equal(0, buyer.ReceivedFeedbackInAuctions.Count);

                _allegroService.DidNotReceiveWithAnyArgs().GivePositiveFeedback(0, 0);
                _scheduler.ReceivedWithAnyArgs(1).Create(null, null);
            }
        }
        [Fact]
        public void Process_AllegroServiceLoginRequired()
        {
            // arrange
            CreateFakeData();

            _allegroService.GetWaitingFeedback().Returns(new List<WaitFeedbackStruct>
            {
                new WaitFeedbackStruct {feItemId = 111, feToUserId = 412, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 1},
                new WaitFeedbackStruct {feItemId = 247, feToUserId = 412, feOp = 2, feAnsCommentType = "POS", fePossibilityToAdd = 1}
            });
            _allegroService.GivePositiveFeedback(111, 412).Returns(100);
            _allegroService.IsLoginRequired(UserId).Returns(true);

            // act
            _processor.Process();

            // assert
            _allegroService.Received(1).Login(UserId, Arg.Is<AllegroCredentials>(t => t.UserName == "username1" && t.Pass == "hashPass1" && t.ApiKey == "allegroKey1"));
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

        protected override void CreateFakeData()
        {
            base.CreateFakeData();
            Buyer buyer = new Buyer
            {
                AllegroUserId = 412
            };
            Order order = new Order
            {
                OrderStatus = OrderStatus.Done,
                Buyer = buyer,
                AllegroDealId = 1867
            };
            Order order2 = new Order
            {
                OrderStatus = OrderStatus.Done,
                Buyer = buyer,
                AllegroDealId = 29545
            };

            Auction ad1 = _db.Auctions.Include(t => t.User).Single(t => t.AllegroAuctionId == 111);
            Auction ad2 = _db.Auctions.Include(t => t.User).Single(t => t.AllegroAuctionId == 247);
            ad1.AutomaticFeedbackEnabled = true;
            ad1.Orders.Add(order);
            ad2.Orders.Add(order2);
            _db.SaveChanges();
        }
    }
}
