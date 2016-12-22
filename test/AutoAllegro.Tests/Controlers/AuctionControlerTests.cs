using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoAllegro.Controllers;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Services;
using AutoAllegro.Services.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace AutoAllegro.Tests.Controlers
{
    public class AuctionControlerTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AuctionController _controler;

        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<User> _userManager;
        private readonly IAllegroService _allegroService;
        private readonly string _userId = "TestUserId";
        private readonly string _userId2 = "TestUserId2";

        public AuctionControlerTests()
        {
            // initialize mocks
            var efServiceProvider = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            var services = new ServiceCollection();
            services.AddOptions();
            services.AddDbContext<ApplicationDbContext>(b => b.UseInMemoryDatabase().UseInternalServiceProvider(efServiceProvider));

            services.AddIdentity<User, IdentityRole>()
                    .AddEntityFrameworkStores<ApplicationDbContext>();

            // IHttpContextAccessor is required for SignInManager, and UserManager
            var authHandler = Substitute.For<IAuthenticationHandler>();
            authHandler.AuthenticateAsync(null).ReturnsForAnyArgs(x => Task.FromResult(0)).AndDoes(t => t.Arg<AuthenticateContext>().NotAuthenticated());

            var context = new DefaultHttpContext();
            context.Features.Set<IHttpAuthenticationFeature>(new HttpAuthenticationFeature { Handler = authHandler });
            services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context });

            services.AddTransient(t => Substitute.For<IAllegroService>());
            services.AddAutoMapper(Startup.ConfigureAutoMapper);

            _serviceProvider = services.BuildServiceProvider();

            // init controler
            _userManager = _serviceProvider.GetRequiredService<UserManager<User>>();
            _dbContext = _serviceProvider.GetRequiredService<ApplicationDbContext>();
            _allegroService = _serviceProvider.GetRequiredService<IAllegroService>();
            var mapper = _serviceProvider.GetRequiredService<IMapper>();
            _controler = new AuctionController(_dbContext, _userManager, _allegroService, mapper, new AllegroProcessor(null, null));
        }

        [Fact]
        public async Task Index_ShouldRetunAuctionList_ForLoggedUser()
        {
            // arange
            CreateFakeData();
            PopulateHttpContext(_userId);

            // act
            IActionResult result = await _controler.Index(null);

            // assert
            Assert.IsType<ViewResult>(result);
            Assert.IsType<IndexViewModel>(((ViewResult) result).Model);

            var model = (IndexViewModel) ((ViewResult) result).Model;
            var auctions = model.Auctions;
            Assert.NotNull(model.PaginationSettings);
            Assert.Equal(2, auctions.Count);

            Assert.Equal(111, auctions[0].AllegroAuctionId);
            Assert.Equal(50.0m, auctions[0].Fee);
            Assert.Equal(new DateTime(2011, 5, 4, 5, 6, 6), auctions[0].EndDate);
            Assert.Equal("test ad", auctions[0].Title);
            Assert.Equal(51.23m, auctions[0].OpenCost);
            Assert.Equal(8.99m, auctions[0].PricePerItem);

            Assert.Equal(7731, auctions[1].AllegroAuctionId);
            Assert.Equal(513m, auctions[1].Fee);
            Assert.Equal(new DateTime(1998, 5, 4, 5, 6, 6), auctions[1].EndDate);
            Assert.Equal("test ad2", auctions[1].Title);
            Assert.Equal(634m, auctions[1].OpenCost);
            Assert.Equal(619m, auctions[1].PricePerItem);
        }
        [Fact]
        public async Task Index_ShouldRetunAuctionList_ForDifferentLoggedUser()
        {
            // arange
            CreateFakeData();
            PopulateHttpContext(_userId2);

            // act
            IActionResult result = await _controler.Index(null);

            // assert
            Assert.IsType<ViewResult>(result);
            Assert.IsType<IndexViewModel>(((ViewResult)result).Model);

            var model = (IndexViewModel)((ViewResult)result).Model;
            var auctions = model.Auctions;
            Assert.NotNull(model.PaginationSettings);
            Assert.Equal(2, auctions.Count);

            Assert.Equal(333, auctions[0].AllegroAuctionId);
            Assert.Equal(5.0m, auctions[0].Fee);
            Assert.Equal(new DateTime(2006, 5, 4, 5, 6, 6), auctions[0].EndDate);
            Assert.Equal("test ad3", auctions[0].Title);
            Assert.Equal(5.23m, auctions[0].OpenCost);
            Assert.Equal(88.99m, auctions[0].PricePerItem);

            Assert.Equal(247, auctions[1].AllegroAuctionId);
            Assert.Equal(7m, auctions[1].Fee);
            Assert.Equal(new DateTime(2007, 5, 4, 2, 3, 6), auctions[1].EndDate);
            Assert.Equal("test ad4", auctions[1].Title);
            Assert.Equal(9.23m, auctions[1].OpenCost);
            Assert.Equal(18.99m, auctions[1].PricePerItem);
        }

        [Fact]
        public async Task Auction_ShouldRedirectToIndex_ForNotExistingAuction()
        {
            // arange
            PopulateHttpContext(_userId);
            CreateFakeData();

            // act
            IActionResult result = await _controler.Auction(3, null, false);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult) result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }

        [Fact]
        public async Task Auction_ShouldReturnAuctionAndOrders_ForExistingAuction1()
        {
            // arange
            PopulateHttpContext(_userId);
            CreateFakeData();

            // act
            IActionResult result = await _controler.Auction(1, null, false);

            // assert
            Assert.IsType<ViewResult>(result);
            ViewResult view = (ViewResult)result;
            AuctionViewModel model = (AuctionViewModel) view.Model;

            Assert.Equal(1, model.Id);
            Assert.Equal("test ad", model.Title);
            Assert.Equal(111, model.AllegroAuctionId);
            Assert.Equal(50, model.Fee);
            Assert.Equal(51.23m, model.OpenCost);
            Assert.Equal(8.99m, model.PricePerItem);
            Assert.Equal(new DateTime(2011, 5, 4, 5, 6, 6), model.EndDate);
            Assert.True(model.IsMonitored);
            Assert.True(model.IsVirtualItem);
            Assert.Equal(2, model.Orders.Count);

            Assert.Equal(1, model.Orders[0].Id);
            Assert.Equal(4, model.Orders[0].Quantity);
            Assert.Equal(4 * 8.99m, model.Orders[0].TotalPayment);
            Assert.Equal(new DateTime(1993, 12, 11, 14, 55, 22), model.Orders[0].OrderDate);
            Assert.Equal(OrderStatus.Send, model.Orders[0].OrderStatus);
            Assert.True(model.Orders[0].VirtualItem);
            Assert.Equal("buyer1@gmail.com", model.Orders[0].Buyer.Email);
            Assert.Equal("Pierdola", model.Orders[0].Buyer.UserLogin);

            Assert.Equal(4, model.Orders[1].Id);
            Assert.Equal(66, model.Orders[1].Quantity);
            Assert.Equal(66 * 8.99m, model.Orders[1].TotalPayment);
            Assert.Equal(new DateTime(2222, 3, 5, 12, 33, 22), model.Orders[1].OrderDate);
            Assert.Equal(OrderStatus.Created, model.Orders[1].OrderStatus);
            Assert.True(model.Orders[1].VirtualItem);
            Assert.Equal("buyer3@gmail.com", model.Orders[1].Buyer.Email);
            Assert.Equal("Tomus", model.Orders[1].Buyer.UserLogin);
        }

        [Fact]
        public async Task Auction_ShouldRefreshFees_ForExistingAuction1()
        {
            // arange
            PopulateHttpContext(_userId);
            CreateFakeData();
            _allegroService.Login(_userId, Arg.Any<Func<AllegroCredentials>>()).Returns(Task.FromResult(true));
            _allegroService.UpdateAuctionFees(Arg.Any<Auction>()).Returns(t => t.Arg<Auction>())
                .AndDoes(t =>
                {
                    Auction auction = t.Arg<Auction>();
                    auction.Fee = 1.0m;
                    auction.OpenCost = 52m;
                });


            // act
            IActionResult result = await _controler.Auction(1, null, true);

            // assert
            Assert.IsType<ViewResult>(result);
            ViewResult view = (ViewResult)result;
            AuctionViewModel model = (AuctionViewModel)view.Model;

            await _allegroService.Received(1).UpdateAuctionFees(Arg.Is<Auction>(t => t.Id == 1));
            await _allegroService.Received(1).Login(_userId, Arg.Is<Func<AllegroCredentials>>(t =>
                t.Invoke().ApiKey == "allegroKey1" && t.Invoke().JournalStart == 14 && t.Invoke().Pass == "hashPass1" && t.Invoke().UserName == "username1"));
            Assert.Equal(1.0m, model.Fee);
            Assert.Equal(52.0m, model.OpenCost);
        }

        [Fact]
        public async Task Auction_ShouldRedirectToIndex_ForModelError()
        {
            // arange
            PopulateHttpContext(_userId);
            CreateFakeData();
            _controler.ModelState.AddModelError("error", "some error");

            // act
            IActionResult result = await _controler.Auction(0, null, false);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }

        private void PopulateHttpContext(string userId)
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var httpContext = _serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            _controler.ControllerContext.HttpContext = httpContext;
        }
        private void CreateFakeData()
        {
            var userManager = _serviceProvider.GetRequiredService<UserManager<User>>();
            userManager.CreateAsync(new User {Id = _userId, UserName = "Test", Email = "test@wp.pl",
                AllegroUserName = "username1", AllegroHashedPass = "hashPass1", AllegroKey = "allegroKey1", AllegroJournalStart = 14}, "Pass@word1").Wait();
            userManager.CreateAsync(new User { Id = _userId2, UserName = "Test2", Email = "test2@gmail.com",
                AllegroUserName = "username2", AllegroHashedPass = "hashPass2", AllegroKey = "allegroKey2", AllegroJournalStart = 14 }, "Pass@word13").Wait();

            _dbContext.Auctions.Add(new Auction
            {
                Id = 1,
                UserId = _userId,
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

            _dbContext.Auctions.Add(new Auction
            {
                Id = 2,
                UserId = _userId,
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

            _dbContext.Auctions.Add(new Auction
            {
                Id = 3,
                UserId = _userId2,
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

            _dbContext.Auctions.Add(new Auction
            {
                Id = 4,
                UserId = _userId2,
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
            _dbContext.Buyers.Add(new Buyer
            {
                Id = 1,
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
            _dbContext.Buyers.Add(new Buyer
            {
                Id = 2,
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
            _dbContext.Buyers.Add(new Buyer
            {
                Id = 3,
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

            _dbContext.Orders.Add(new Order
            {
                Id = 1,
                AuctionId = 1,
                BuyerId = 1,
                OrderDate = new DateTime(1993, 12, 11, 14, 55, 22),
                OrderStatus = OrderStatus.Send,
                Quantity = 4
            });
            _dbContext.Orders.Add(new Order
            {
                Id = 2,
                AuctionId = 2,
                BuyerId = 1,
                OrderDate = new DateTime(1991, 12, 11, 12, 55, 22),
                OrderStatus = OrderStatus.Paid,
                Quantity = 2
            });
            _dbContext.Orders.Add(new Order
            {
                Id = 3,
                AuctionId = 3,
                BuyerId = 2,
                OrderDate = new DateTime(1995, 12, 5, 12, 33, 22),
                OrderStatus = OrderStatus.Done,
                Quantity = 1
            });
            _dbContext.Orders.Add(new Order
            {
                Id = 4,
                AuctionId = 1,
                BuyerId = 3,
                OrderDate = new DateTime(2222, 3, 5, 12, 33, 22),
                OrderStatus = OrderStatus.Created,
                Quantity = 66
            });
            _dbContext.Orders.Add(new Order
            {
                Id = 5,
                AuctionId = 4,
                BuyerId = 3,
                OrderDate = new DateTime(2012, 3, 3, 12, 33, 11),
                OrderStatus = OrderStatus.Done,
                Quantity = 3
            });

            _dbContext.SaveChanges();
        }
    }
}
