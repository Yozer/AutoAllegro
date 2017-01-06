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
using Xunit;

namespace AutoAllegro.Tests.Services
{
    public class AllegroRefundProcessorTests : DatabaseMock
    {
        private readonly IBackgroundJobClient _scheduler;
        private readonly IAllegroRefundProcessor _processor;
        private readonly IAllegroService _allegroService;
        private readonly IServiceScope _scope;
        private ApplicationDbContext _db;

        public AllegroRefundProcessorTests()
        {
            _scope = CreateScope();
            _scheduler = _scope.ServiceProvider.GetService<IBackgroundJobClient>();
            _allegroService = _scope.ServiceProvider.GetService<IAllegroService>();
            _db = GetDatabase(_scope);

            _processor = new AllegroRefundProcessor(_scheduler, _allegroService, Substitute.For<ILogger<AllegroRefundProcessor>>(), _db);
        }

        [Fact]
        public void Process_MakeRefunds()
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
                OrderDate = DateTime.Now.Subtract(TimeSpan.FromDays(12)),
                OrderStatus = OrderStatus.Created,
                Quantity = 1,
                Buyer = buyer
            };
            Order order2 = new Order
            {
                AllegroDealId = 513,
                OrderDate = DateTime.Now.Subtract(TimeSpan.FromDays(12)),
                OrderStatus = OrderStatus.Created,
                Quantity = 55,
                Buyer = buyer
            };
            Order order3 = new Order
            {
                AllegroDealId = 514,
                OrderDate = DateTime.Now.Subtract(TimeSpan.FromDays(12)),
                OrderStatus = OrderStatus.Created,
                Quantity = 44,
                Buyer = buyer
            };
            Order order4 = new Order
            {
                AllegroDealId = 515,
                OrderDate = DateTime.Now.Subtract(TimeSpan.FromDays(12)),
                OrderStatus = OrderStatus.Created,
                Quantity = 1,
                Buyer = buyer
            };
            Order order5= new Order
            {
                AllegroDealId = 516,
                OrderDate = DateTime.Now.Subtract(TimeSpan.FromDays(12)),
                OrderStatus = OrderStatus.Paid,
                Quantity = 1,
                Buyer = buyer
            };
            Auction ad1 = _db.Auctions.Include(t => t.User).Single(t => t.AllegroAuctionId == 111);
            Auction ad2 = _db.Auctions.Include(t => t.User).Single(t => t.AllegroAuctionId == 247);
            Auction ad3 = _db.Auctions.Include(t => t.User).Single(t => t.AllegroAuctionId == 333);
            ad1.Orders.Add(order);
            ad1.Orders.Add(order2);
            ad2.Orders.Add(order3);
            ad3.Orders.Add(order4);
            ad3.Orders.Add(order5);
            _db.SaveChanges();

            _allegroService.SendRefund(Arg.Is<Order>(t => t.AllegroDealId == 512), 1).Returns(100);
            _allegroService.SendRefund(Arg.Is<Order>(t => t.AllegroDealId == 513), 1).Returns(101);
            _allegroService.SendRefund(Arg.Is<Order>(t => t.AllegroDealId == 515), 1).Returns(103);

            // act
            _processor.Process();

            // assert
            using (var scope = _scope.ServiceProvider.GetService<IServiceScopeFactory>().CreateScope())
            {
                var database = GetDatabase(scope);

                order = database.Orders.Include(t => t.GameCodes).First(t => t.AllegroDealId == 512);
                order2 = database.Orders.Include(t => t.GameCodes).First(t => t.AllegroDealId == 513);
                order3 = database.Orders.Include(t => t.GameCodes).First(t => t.AllegroDealId == 514);
                order4 = database.Orders.Include(t => t.GameCodes).First(t => t.AllegroDealId == 515);
                order5 = database.Orders.Include(t => t.GameCodes).First(t => t.AllegroDealId == 516);

                Assert.Equal(OrderStatus.Canceled, order.OrderStatus);
                Assert.Equal(100, order.AllegroRefundId);
                Assert.Equal(OrderStatus.Canceled, order2.OrderStatus);
                Assert.Equal(101, order2.AllegroRefundId);
                Assert.Equal(OrderStatus.Created, order3.OrderStatus);
                Assert.Null(order3.AllegroRefundId);
                Assert.Equal(OrderStatus.Canceled, order4.OrderStatus);
                Assert.Equal(103, order4.AllegroRefundId);
                Assert.Equal(OrderStatus.Paid, order5.OrderStatus);
                Assert.Null(order5.AllegroRefundId);
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
