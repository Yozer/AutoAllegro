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

            var httpContext = PopulateContext();
            _controler.ControllerContext.HttpContext = httpContext;
        }

        private HttpContext PopulateContext()
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, _userId) };
            var userManager = _serviceProvider.GetRequiredService<UserManager<User>>();
            userManager.CreateAsync(new User { Id = _userId, UserName = "Test", Email = "test@wp.pl" }, "Pass@word1").Wait();
            userManager.CreateAsync(new User { Id = _userId2, UserName = "Test2", Email = "test2@gmail.com" }, "Pass@word13").Wait();

            var httpContext = _serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));

            return httpContext;
        }

        [Fact]
        public async Task IndexShouldRetunAuctionListForLoggedUser()
        {
            // arange
            CreateFakeData();

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

        private void CreateFakeData()
        {
            _dbContext.Auctions.Add(new Auction
            {
                UserId = _userId,
                AllegroAuctionId = 111,
                CreationDate = new DateTime(2002, 12, 3, 5, 4, 2),
                EndDate = new DateTime(2011, 5, 4, 5, 6, 6),
                Fee = 50.0m,
                Title = "test ad",
                OpenCost = 51.23m,
                PricePerItem = 8.99m
            });

            _dbContext.Auctions.Add(new Auction
            {
                UserId = _userId,
                AllegroAuctionId = 7731,
                CreationDate = new DateTime(1994, 12, 3, 5, 4, 2),
                EndDate = new DateTime(1998, 5, 4, 5, 6, 6),
                Fee = 513,
                Title = "test ad2",
                OpenCost = 634,
                PricePerItem = 619m
            });

            _dbContext.Auctions.Add(new Auction
            {
                UserId = _userId2,
                AllegroAuctionId = 333,
                CreationDate = new DateTime(2005, 12, 3, 5, 4, 2),
                EndDate = new DateTime(2006, 5, 4, 5, 6, 6),
                Fee = 5,
                Title = "test ad3",
                OpenCost = 5.23m,
                PricePerItem = 88.99m
            });


            _dbContext.Auctions.Add(new Auction
            {
                UserId = _userId2,
                AllegroAuctionId = 247,
                CreationDate = new DateTime(2004, 12, 3, 5, 4, 3),
                EndDate = new DateTime(2007, 5, 4, 2, 3, 6),
                Fee = 7,
                Title = "test ad4",
                OpenCost = 9.23m,
                PricePerItem = 18.99m
            });

            _dbContext.SaveChanges();
        }
    }
}
