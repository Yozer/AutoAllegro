using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoAllegro.Controllers;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Services;
using AutoAllegro.Services.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace AutoAllegro.Tests.Controllers
{
    public class AuctionControllerTests : DatabaseMock
    {
        private readonly IAllegroService _allegroService;
        private readonly IAllegroProcessor _allegroProcessor;
        private readonly IMapper _mapper;

        public AuctionControllerTests()
        {
            Services.AddTransient(t => Substitute.For<IAllegroService>());
            Services.AddSingleton(t => Substitute.For<IAllegroProcessor>());

            ServiceProvider = Services.BuildServiceProvider();

            InitDatabase();

                // init controller
            _allegroService = ServiceProvider.GetRequiredService<IAllegroService>();
            _allegroProcessor = ServiceProvider.GetRequiredService<IAllegroProcessor>();
            _mapper = ServiceProvider.GetRequiredService<IMapper>();
        }

        [Fact]
        public async Task Index_ShouldReturnAuctionList_ForLoggedUser()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Index(null);
            }

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
        public async Task Index_ShouldReturnAuctionList_ForDifferentLoggedUser()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId2, controller, scope);
                result = await controller.Index(null);
            }

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
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Auction(3, null, false, null, false);
            }

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult) result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }

        [Fact]
        public async Task Auction_ShouldReturnAuctionAndOrders_ForExistingAuction1()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Auction(1, null, false, null, false);
            }

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

            Assert.Equal(1, model.Orders[0].AuctionId);
            Assert.Equal(4, model.Orders[0].Quantity);
            Assert.Equal(4 * 8.99m, model.Orders[0].TotalPayment);
            Assert.Equal(new DateTime(1993, 12, 11, 14, 55, 22), model.Orders[0].OrderDate);
            Assert.Equal(OrderStatus.Send, model.Orders[0].OrderStatus);
            Assert.True(model.Orders[0].VirtualItem);
            Assert.Equal("buyer1@gmail.com", model.Orders[0].Buyer.Email);
            Assert.Equal("Pierdola", model.Orders[0].Buyer.UserLogin);

            Assert.Equal(1, model.Orders[1].AuctionId);
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
            // arrange
            CreateFakeData();
            _allegroService.IsLoginRequired(UserId).Returns(true);
            _allegroService.Login(UserId, Arg.Any<AllegroCredentials>()).Returns(Task.FromResult(true));
            _allegroService.UpdateAuctionFees(Arg.Any<Auction>()).Returns(t => t.Arg<Auction>())
                .AndDoes(t =>
                {
                    Auction auction = t.Arg<Auction>();
                    auction.Fee = 1.0m;
                    auction.OpenCost = 52m;
                });


            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Auction(1, null, true, null, false);
            }

            // assert
            Assert.IsType<ViewResult>(result);
            ViewResult view = (ViewResult)result;
            AuctionViewModel model = (AuctionViewModel)view.Model;

            await _allegroService.Received(1).UpdateAuctionFees(Arg.Is<Auction>(t => t.Id == 1));
            await _allegroService.Received(1).Login(UserId, Arg.Is<AllegroCredentials>(t =>
                t.ApiKey == "allegroKey1" && t.JournalStart == 14 && t.Pass == "hashPass1" && t.UserName == "username1"));
            Assert.Equal(1.0m, model.Fee);
            Assert.Equal(52.0m, model.OpenCost);
        }

        [Fact]
        public async Task Auction_ShouldRedirectToIndex_ForModelError()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                controller.ModelState.AddModelError("error", "some error");
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Auction(0, null, false, null, false);
            }

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }

        [Fact]
        public async Task AuctionPost_ShouldRedirectToIndex_ForModelError()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                controller.ModelState.AddModelError("error", "some error");
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Auction(null);
            }

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }
        [Fact]
        public async Task AuctionPost_ShouldRedirectToIndex_ForNotExistingAuction()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId2, controller, scope);
                result = await controller.Auction(new AuctionViewModel { Id = 6});
            }

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }

        [Fact]
        public async Task AuctionPost_ShouldRedirectToIndex_ForAuctionThatBelongsToDifferentUser()
        {
            // arange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId2, controller, scope);
                result = await controller.Auction(new AuctionViewModel { Id = 1 });
            }

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }

        [Fact]
        public async Task AuctionPost_ShouldDisableMonitoring_ForAuction1()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Auction(new AuctionViewModel { Id = 1, IsMonitored = false, IsVirtualItem = true });
            }

            // assert
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);

                Assert.IsType<RedirectToActionResult>(result);
                RedirectToActionResult redirect = (RedirectToActionResult) result;
                Auction auction = database.Auctions.Single(t => t.Id == 1);

                Assert.False(auction.IsMonitored);
                Assert.Equal("Auction", redirect.ActionName);
                Assert.Equal(1, redirect.RouteValues["id"]);
                Assert.Equal(true, redirect.RouteValues["settingsTabActive"]);
                _allegroProcessor.Received(1).StopProcessor(Arg.Is<Auction>(t => t.Id == 1 && !t.IsMonitored));
                _allegroProcessor.DidNotReceive().StartProcessor(Arg.Any<Auction>());
            }
        }

        [Fact]
        public async Task AuctionPost_ShouldEnableMonitoringAndDisableVirtualItem_ForAuction2()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Auction(new AuctionViewModel { Id = 2, IsMonitored = true, IsVirtualItem = false });
            }

            // assert
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                Assert.IsType<RedirectToActionResult>(result);
                RedirectToActionResult redirect = (RedirectToActionResult) result;
                Auction auction = database.Auctions.Single(t => t.Id == 2);

                Assert.False(auction.IsVirtualItem);
                Assert.True(auction.IsMonitored);
                Assert.Equal("Auction", redirect.ActionName);
                Assert.Equal(2, redirect.RouteValues["id"]);
                Assert.Equal(true, redirect.RouteValues["settingsTabActive"]);
                Assert.Equal(AuctionMessageId.Success, redirect.RouteValues["message"]);
                _allegroProcessor.Received(1).StartProcessor(Arg.Is<Auction>(t => t.Id == 2 && t.IsMonitored));
                _allegroProcessor.DidNotReceive().StopProcessor(Arg.Any<Auction>());
            }
        }

        [Fact]
        public async Task AuctionPost_ShouldNotAllowToSelectVirtualItem_ForAuction3()
        {
            // arrange
            Auction ad;
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                ad = database.Auctions.First(t => t.AllegroAuctionId == 333);
                var controller = new AuctionController(database, GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId2, controller, scope);
                result = await controller.Auction(new AuctionViewModel { Id = ad.Id, IsMonitored = true, IsVirtualItem = true });
            }

            // assert
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                Assert.IsType<RedirectToActionResult>(result);
                RedirectToActionResult redirect = (RedirectToActionResult)result;
                Auction auction = database.Auctions.Single(t => t.Id == ad.Id);

                Assert.False(auction.IsVirtualItem);
                Assert.True(auction.IsMonitored);
                Assert.Equal("Auction", redirect.ActionName);
                Assert.Equal(ad.Id, redirect.RouteValues["id"]);
                Assert.Equal(AuctionMessageId.CannotSetVirtualItem, redirect.RouteValues["message"]);
                Assert.Equal(true, redirect.RouteValues["settingsTabActive"]);
                _allegroProcessor.DidNotReceiveWithAnyArgs().StartProcessor(null);
                _allegroProcessor.DidNotReceiveWithAnyArgs().StopProcessor(null);
            }
        }

        [Fact]
        public async Task AuctionPost_ShouldNotChangeMonitoringSetting_ForAuction4()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId2, controller, scope);
                result = await controller.Auction(new AuctionViewModel { Id = 4, IsMonitored = false, IsVirtualItem = false });
            }

            // assert
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                Assert.IsType<RedirectToActionResult>(result);
                RedirectToActionResult redirect = (RedirectToActionResult) result;
                Auction auction = database.Auctions.Single(t => t.Id == 4);

                Assert.False(auction.IsMonitored);
                Assert.Equal("Auction", redirect.ActionName);
                Assert.Equal(4, redirect.RouteValues["id"]);
                Assert.Equal(true, redirect.RouteValues["settingsTabActive"]);
                Assert.Equal(AuctionMessageId.Success, redirect.RouteValues["message"]);
                _allegroProcessor.DidNotReceive().StartProcessor(Arg.Any<Auction>());
                _allegroProcessor.DidNotReceive().StopProcessor(Arg.Any<Auction>());
            }
        }

        [Fact]
        public async Task Order_ShouldRedirectToIndex_ForModelError()
        {
            // arange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                controller.ModelState.AddModelError("error", "some error");
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Auction(new AuctionViewModel { Id = 4, IsMonitored = false, IsVirtualItem = false });
            }

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }

        [Fact]
        public async Task Order_ShouldRedirectToIndex_ForNotExistingOrder()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Order(-1);
            }

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }

        [Fact]
        public async Task Order_ShouldRedirectToIndex_ForNotUserOrder()
        {
            // arrange
            int orderId;
            CreateFakeData();
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                orderId = database.Orders.First(t => t.Auction.UserId == UserId2).Id;
            }
            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Order(orderId);
            }

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }

        [Fact]
        public async Task Order_ShouldReturnOrderView_ForOrder1()
        {
            // arrange
            int orderId;
            CreateFakeData();
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                orderId = database.Orders.First(t => t.AllegroDealId == 2).Id;
            }

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Order(orderId);
            }

            // assert
            Assert.IsType<ViewResult>(result);
            Assert.IsType<OrderViewModel>(((ViewResult)result).Model);

            ViewResult view = (ViewResult)result;
            OrderViewModel model = (OrderViewModel) view.Model;

            Assert.Equal(orderId, model.Id);
            Assert.Equal(2, model.Quantity);
            Assert.Equal(2*619m, model.TotalPayment);
            Assert.Equal(new DateTime(1991, 12, 11, 12, 55, 22), model.OrderDate);
            Assert.Equal(OrderStatus.Paid, model.OrderStatus);
            Assert.Equal(1, model.Buyer.Id);
            Assert.True(model.VirtualItem);
            Assert.Equal(1, model.ShippingAddress.Id);
            Assert.Equal("Some addr", model.ShippingAddress.Address);
            Assert.Equal("Some city", model.ShippingAddress.City);
            Assert.Equal("First name", model.ShippingAddress.FirstName);
            Assert.Equal("Last name", model.ShippingAddress.LastName);
            Assert.Equal("Some msg", model.ShippingAddress.MessageToSeller);
            Assert.Equal("33-300", model.ShippingAddress.PostCode);
        }

        [Fact]
        public async Task Add_ShouldFetch_TwoNewAuctions()
        {
            // arrange
            CreateFakeData();
            _allegroService.IsLoginRequired(UserId).Returns(false);
            _allegroService.GetNewAuctions().Returns(Task.FromResult(new List<NewAuction>
            {
                new NewAuction
                {
                    Id = 1261,
                    StartDate = new DateTime(2012, 12, 3),
                    EndDate = new DateTime(2013, 5, 3),
                    Name = "test addd",
                    Price = 55.1m
                },
                new NewAuction
                {
                    Id = 1263,
                    StartDate = new DateTime(2015, 12, 3),
                    EndDate = new DateTime(2016, 5, 3),
                    Name = "test addd2",
                    Price = 551.1m
                },
                new NewAuction { Id = 111 },
                new NewAuction { Id = 7731 }
            }));


            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Add(true);
            }

            // assert
            Assert.IsType<ViewResult>(result);
            Assert.IsType<AddViewModel>(((ViewResult)result).Model);

            AddViewModel model = (AddViewModel) ((ViewResult) result).Model;

            Assert.Equal(2, model.Auctions.Count);
            Assert.Equal(1261, model.Auctions[0].Id);
            Assert.Equal(1263, model.Auctions[1].Id);
            await _allegroService.DidNotReceiveWithAnyArgs().Login(null, Arg.Any<AllegroCredentials>());
            await _allegroService.Received(1).GetNewAuctions();
        }
        [Fact]
        public async Task AddPost_ShouldRedirectToIndex_ForModelError()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                controller.ModelState.AddModelError("error", "some error");
                result = await controller.Add(new AddViewModel());
            }

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }
        [Fact]
        public async Task AddPost_ShouldSaveAndUpdateFees_ForOneSelectedAuction()
        {
            // arrange
            CreateFakeData();
            _allegroService.IsLoginRequired(UserId).Returns(true);
            _allegroService.UpdateAuctionFees(Arg.Is<Auction>(t => t.AllegroAuctionId == 1261)).Returns(t => t.Arg<Auction>())
                .AndDoes(t =>
                {
                    Auction auction = t.Arg<Auction>();
                    auction.Fee = 1.0m;
                    auction.OpenCost = 52m;
                });

            var model = new AddViewModel
            {
                Auctions = new List<NewAuction>
                {
                    new NewAuction
                    {
                        Id = 1261,
                        StartDate = new DateTime(2012, 12, 3),
                        EndDate = new DateTime(2013, 5, 3),
                        Name = "test addd",
                        Price = 55.1m,
                        ShouldBeSaved = true
                    },
                    new NewAuction
                    {
                        Id = 1263,
                        StartDate = new DateTime(2015, 12, 3),
                        EndDate = new DateTime(2016, 5, 3),
                        Name = "test addd2",
                        Price = 551.1m,
                        ShouldBeSaved = false
                    }
                }
            };

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Add(model);
            }

            // assert
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                Assert.IsType<RedirectToActionResult>(result);
                var redirect = (RedirectToActionResult) result;
                Auction ad = database.Auctions.Single(t => t.AllegroAuctionId == 1261);

                Assert.Equal(1, ad.Converter);
                Assert.Equal(new DateTime(2012, 12, 3), ad.CreationDate);
                Assert.Equal(new DateTime(2013, 5, 3), ad.EndDate);
                Assert.False(ad.IsMonitored);
                Assert.False(ad.IsVirtualItem);
                Assert.Equal(55.1m, ad.PricePerItem);
                Assert.Equal("test addd", ad.Title);
                Assert.Equal(UserId, ad.UserId);
                Assert.Equal(1m, ad.Fee);
                Assert.Equal(52m, ad.OpenCost);
                Assert.Null(redirect.ControllerName);
                Assert.Equal("Index", redirect.ActionName);

                await _allegroService.Received(1).Login(UserId, Arg.Any<AllegroCredentials>());
                await _allegroService.Received(1).UpdateAuctionFees(Arg.Is<Auction>(t => t.AllegroAuctionId == 1261));
            }
        }

        [Fact]
        public async Task Add_ShouldNotFetch()
        {
            // arrange
            CreateFakeData();

            // act
            IActionResult result;
            using (var scope = CreateScope())
            {
                var controller = new AuctionController(GetDatabase(scope), GetUserManager(scope), _allegroService, _mapper, _allegroProcessor);
                PopulateHttpContext(UserId, controller, scope);
                result = await controller.Add(false);
            }

            // assert
            Assert.IsType<ViewResult>(result);
            Assert.IsType<AddViewModel>(((ViewResult)result).Model);
            await _allegroService.DidNotReceiveWithAnyArgs().GetNewAuctions();
        }
        private void PopulateHttpContext(string userId, AuctionController controller, IServiceScope scope)
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var httpContext = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            controller.ControllerContext.HttpContext = httpContext;
        }
    }
}
