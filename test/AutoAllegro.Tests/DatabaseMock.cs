﻿using System;
using System.Threading.Tasks;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace AutoAllegro.Tests
{
    public abstract class DatabaseMock
    {
        protected ServiceCollection Services;
        protected IServiceProvider ServiceProvider;

        protected const string UserId = "TestUserId";
        protected const string UserId2 = "TestUserId2";

        protected DatabaseMock()
        {
            // initialize mocks
            var efServiceProvider = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            Services = new ServiceCollection();
            Services.AddOptions();
            Services.AddDbContext<ApplicationDbContext>(b => b.UseInMemoryDatabase().UseInternalServiceProvider(efServiceProvider));

            Services.AddIdentity<User, IdentityRole>()
                    .AddEntityFrameworkStores<ApplicationDbContext>();

            // IHttpContextAccessor is required for SignInManager, and UserManager
            var authHandler = Substitute.For<IAuthenticationHandler>();
            authHandler.AuthenticateAsync(null).ReturnsForAnyArgs(x => Task.FromResult(0)).AndDoes(t => t.Arg<AuthenticateContext>().NotAuthenticated());

            var context = new DefaultHttpContext();
            context.Features.Set<IHttpAuthenticationFeature>(new HttpAuthenticationFeature { Handler = authHandler });
            Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context });

            // custom
            Services.AddAutoMapper(Startup.ConfigureAutoMapper);
        }

        protected IServiceScope CreateScope()
        {
            return ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        }

        protected ApplicationDbContext GetDatabase(IServiceScope scope)
        {
            return scope.ServiceProvider.GetService<ApplicationDbContext>();
        }
        protected UserManager<User> GetUserManager(IServiceScope scope)
        {
            return scope.ServiceProvider.GetService<UserManager<User>>();
        }
        protected virtual void CreateFakeData()
        {
            using (var scope = CreateScope())
            {
                var userManager = GetUserManager(scope);
                var database = GetDatabase(scope);

                userManager.CreateAsync(new User
                {
                    Id = UserId,
                    UserName = "Test",
                    Email = "test@wp.pl",
                    AllegroUserName = "username1",
                    AllegroHashedPass = "hashPass1",
                    AllegroKey = "allegroKey1",
                    AllegroJournalStart = 14
                }, "Pass@word1").Wait();
                userManager.CreateAsync(new User
                {
                    Id = UserId2,
                    UserName = "Test2",
                    Email = "test2@gmail.com",
                    AllegroUserName = "username2",
                    AllegroHashedPass = "hashPass2",
                    AllegroKey = "allegroKey2",
                    AllegroJournalStart = 16
                }, "Pass@word13").Wait();

                database.Auctions.Add(new Auction
                {
                    UserId = UserId,
                    AllegroAuctionId = 111,
                    CreationDate = new DateTime(2002, 12, 3, 5, 4, 2),
                    EndDate = new DateTime(2011, 5, 4, 5, 6, 6),
                    Fee = 50.0m,
                    Title = "test ad",
                    OpenCost = 51.23m,
                    PricePerItem = 8.99m,
                    IsMonitored = true,
                    IsVirtualItem = true,
                    Converter = 1
                });
                database.Auctions.Add(new Auction
                {
                    UserId = UserId,
                    AllegroAuctionId = 7731,
                    CreationDate = new DateTime(1994, 12, 3, 5, 4, 2),
                    EndDate = new DateTime(1998, 5, 4, 5, 6, 6),
                    Fee = 513,
                    Title = "test ad2",
                    OpenCost = 634,
                    PricePerItem = 619m,
                    IsMonitored = false,
                    IsVirtualItem = true,
                    Converter = 5
                });

                database.Auctions.Add(new Auction
                {
                    UserId = UserId2,
                    AllegroAuctionId = 333,
                    CreationDate = new DateTime(2005, 12, 3, 5, 4, 2),
                    EndDate = new DateTime(2006, 5, 4, 5, 6, 6),
                    Fee = 5,
                    Title = "test ad3",
                    OpenCost = 5.23m,
                    PricePerItem = 88.99m,
                    IsMonitored = true,
                    IsVirtualItem = false,
                });

                database.Auctions.Add(new Auction
                {
                    UserId = UserId2,
                    AllegroAuctionId = 247,
                    CreationDate = new DateTime(2004, 12, 3, 5, 4, 3),
                    EndDate = new DateTime(2007, 5, 4, 2, 3, 6),
                    Fee = 7,
                    Title = "test ad4",
                    OpenCost = 9.23m,
                    PricePerItem = 18.99m,
                    IsMonitored = false,
                    IsVirtualItem = false
                });

                // buyers
                database.Buyers.Add(new Buyer
                {
                    Email = "buyer1@gmail.com",
                    Address = "Address1",
                    AllegroUserId = 123,
                    City = "CityBuyer1",
                    FirstName = "Jacek",
                    LastName = "Wojnicz",
                    Phone = "141-141-2",
                    PostCode = "33-114",
                    UserLogin = "Pierdola"
                });
                database.Buyers.Add(new Buyer
                {
                    Email = "buyer2@gmail.com",
                    Address = "Address2",
                    AllegroUserId = 124,
                    City = "CityBuyer2",
                    FirstName = "Marcin",
                    LastName = "Nalepa",
                    Phone = "997",
                    PostCode = "33-300",
                    UserLogin = "Leszczu"
                });
                database.Buyers.Add(new Buyer
                {
                    Email = "buyer3@gmail.com",
                    Address = "Address3",
                    AllegroUserId = 125,
                    City = "CityBuyer3",
                    FirstName = "Tomasz",
                    LastName = "Mniszek",
                    Phone = "516-512-666",
                    PostCode = "11-111",
                    UserLogin = "Tomus"
                });

                database.Orders.Add(new Order
                {
                    AuctionId = 1,
                    BuyerId = 1,
                    OrderDate = new DateTime(1993, 12, 11, 14, 55, 22),
                    OrderStatus = OrderStatus.Send,
                    Quantity = 4
                });
                database.Orders.Add(new Order
                {
                    AuctionId = 2,
                    BuyerId = 1,
                    OrderDate = new DateTime(1991, 12, 11, 12, 55, 22),
                    OrderStatus = OrderStatus.Paid,
                    Quantity = 2,
                    ShippingAddress = new ShippingAddress
                    {
                        Id = 1,
                        Address = "Some addr",
                        City = "Some city",
                        FirstName = "First name",
                        LastName = "Last name",
                        MessageToSeller = "Some msg",
                        PostCode = "33-300"
                    }
                });
                database.Orders.Add(new Order
                {
                    AuctionId = 3,
                    BuyerId = 2,
                    OrderDate = new DateTime(1995, 12, 5, 12, 33, 22),
                    OrderStatus = OrderStatus.Done,
                    Quantity = 1
                });
                database.Orders.Add(new Order
                {
                    AuctionId = 1,
                    BuyerId = 3,
                    OrderDate = new DateTime(2222, 3, 5, 12, 33, 22),
                    OrderStatus = OrderStatus.Created,
                    Quantity = 66
                });
                database.Orders.Add(new Order
                {
                    AuctionId = 4,
                    BuyerId = 3,
                    OrderDate = new DateTime(2012, 3, 3, 12, 33, 11),
                    OrderStatus = OrderStatus.Done,
                    Quantity = 3
                });

                database.SaveChanges();
            }
        }
    }
}