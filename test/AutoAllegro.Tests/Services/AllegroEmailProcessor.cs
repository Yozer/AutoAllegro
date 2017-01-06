using System;
using System.Collections.Generic;
using System.Linq;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Services;
using AutoAllegro.Services.Interfaces;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AutoAllegro.Tests.Services
{
    public class AllegroEmailProcessorTests : DatabaseMock
    {
        private readonly IBackgroundJobClient _scheduler;
        private readonly IAllegroEmailProcessor _processor;
        private readonly IEmailSender _emailSender;
        private readonly IServiceScope _scope;
        private ApplicationDbContext _db;

        public AllegroEmailProcessorTests()
        {
            _scope = CreateScope();
            _scheduler = _scope.ServiceProvider.GetService<IBackgroundJobClient>();
            _emailSender = _scope.ServiceProvider.GetService<IEmailSender>();
            _db = GetDatabase(_scope);

            _processor = new AllegroEmailProcessor(_scheduler, _emailSender, Substitute.For<ILogger<AllegroEmailProcessor>>(), _db);
        }

        [Fact]
        public void Process_SendCodesToBuyer()
        {
            // arrange
            CreateFakeData();
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
                Quantity = 55
            };
            Auction ad = _db.Auctions.Include(t => t.User).Single(t => t.AllegroAuctionId == 111);
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
                MessageSubject = "subject",
                MessageTemplate = "Hi {FIRST_NAME} {LAST_NAME}! You bought {QUANTITY}.<br>Your codes:<br>{ITEM}",
                ReplyTo = "www@wp.pl"
            };

            order.Buyer = buyer;
            order2.Buyer = buyer;
            _db.SaveChanges();

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);
                _emailSender.Received(1).SendEmailAsync("wp@wp.pl", "subject", "Hi firstName lastName! You bought 1.<br>Your codes:<br>xxx<br>yyy", "www@wp.pl", "DisplayName");

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
            Auction ad = _db.Auctions.Include(t => t.User).Single(t => t.AllegroAuctionId == 111);
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
            _db.SaveChanges();

            _emailSender.SendEmailAsync(null, null, null).ThrowsForAnyArgs(new Exception());

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);
                _emailSender.Received(1).SendEmailAsync("wp@wp.pl", "subject", "Hi firstName lastName! You bought 1.<br>Your codes:<br>xxx");

                order = database.Orders.Include(t => t.GameCodes).First(t => t.AllegroDealId == 512);
                var codes = database.GameCodes.Include(t => t.Order).ToList();

                Assert.Equal(0, order.GameCodes.Count);
                Assert.Equal(OrderStatus.Paid, order.OrderStatus);
                Assert.Equal(1, codes.Count(t => t.Order == null));
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
