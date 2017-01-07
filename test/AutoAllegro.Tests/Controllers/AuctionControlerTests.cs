using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AutoAllegro.Tests.Controllers
{
    public sealed class AuctionControllerTests : DatabaseMock
    {
        private readonly IAllegroService _allegroService;
        private readonly IMapper _mapper;
        private readonly ApplicationDbContext _db;
        private readonly AuctionController _controller;
        private readonly IServiceScope _scope;

        public AuctionControllerTests()
        {
            _scope = CreateScope();
            _allegroService = _scope.ServiceProvider.GetRequiredService<IAllegroService>();
            _mapper = _scope.ServiceProvider.GetRequiredService<IMapper>();
            _db = GetDatabase(_scope);
            var userManager = GetUserManager(_scope);

            CreateFakeData();
            _controller = new AuctionController(_db, userManager, _allegroService, _mapper);
        }

        [Fact]
        public async Task Index_ShouldReturnAuctionList_ForLoggedUser()
        {
            // arrange
            PopulateHttpContext(UserId);

            // act
            var result = await _controller.Index(null);

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
            PopulateHttpContext(UserId2);

            // act
            IActionResult result = await _controller.Index(null);

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
            PopulateHttpContext(UserId);

            // act
            var result = await _controller.Auction(3, null, false, null, false);

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
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.Auction(1, null, false, null, false);

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
            Assert.Equal(1, model.FreeCodesCount);
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
        public async Task Codes_ShouldReturnFreeCodesList()
        {
            // arrange
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.Codes(1, null, null);

            // assert
            Assert.IsType<ViewResult>(result);
            ViewResult view = (ViewResult)result;
            CodesViewModel model = (CodesViewModel)view.Model;

            Assert.Equal(1, model.Codes.Count);
            Assert.Equal(1, model.AuctionId);
            Assert.Equal("test ad", model.Title);
            Assert.Null(model.Message);
            Assert.Equal("yyy", model.Codes[0].Code);
            Assert.Equal(new DateTime(2012, 5, 5), model.Codes[0].AddDate);
            Assert.Equal(1, model.Codes[0].AuctionId);
        }
        [Fact]
        public async Task Codes_ShouldRedirectToIndex_NotOurAd()
        {
            // arrange
            PopulateHttpContext(UserId2);

            // act
            IActionResult result = await _controller.Codes(1, null, null);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", ((RedirectToActionResult)result).ActionName);
        }
        [Fact]
        public async Task MarkAsPaid_ShouldRedirectToIndex_NotOurAd()
        {
            // arrange
            var order = _db.Orders.Single(t => t.AllegroDealId == 4);
            PopulateHttpContext(UserId2);

            // act
            IActionResult result = await _controller.MarkAsPaid(order.Id);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", ((RedirectToActionResult)result).ActionName);
        }
        [Fact]
        public async Task MarkAsPaid_ShouldRedirectToIndex_OrderStateOtherThanCreated()
        {
            // arrange
            var order = _db.Orders.Single(t => t.AllegroDealId == 1);
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.MarkAsPaid(order.Id);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", ((RedirectToActionResult)result).ActionName);
        }
        [Fact]
        public async Task MarkAsPaid_ShouldChangeOrderStateToPaid_WithRefund()
        {
            // arrange
            var order = _db.Orders.Single(t => t.AllegroDealId == 4);
            order.AllegroRefundId = 512;
            _db.SaveChanges();
            _allegroService.CancelRefund(512).Returns(true);
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.MarkAsPaid(order.Id);

            // assert

            order = _db.Orders.Single(t => t.AllegroDealId == 4);
            Assert.Equal(OrderStatus.Paid, order.OrderStatus);
            Assert.Null(order.AllegroRefundId);

            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Order", redirect.ActionName);
            Assert.Equal(order.Id, redirect.RouteValues["id"]);
            Assert.Equal(OrderViewMessage.OrderMarkedAsPaid, redirect.RouteValues["message"]);
        }
        [Fact]
        public async Task MarkAsPaid_ShouldNotChangeOrderStatus_WithRefund()
        {
            // arrange
            var order = _db.Orders.Single(t => t.AllegroDealId == 4);
            order.AllegroRefundId = 512;
            _db.SaveChanges();
            _allegroService.CancelRefund(512).Returns(false);
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.MarkAsPaid(order.Id);

            // assert
            order = _db.Orders.Single(t => t.AllegroDealId == 4);
            Assert.Equal(OrderStatus.Created, order.OrderStatus);
            Assert.Equal(512, order.AllegroRefundId);

            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Order", redirect.ActionName);
            Assert.Equal(order.Id, redirect.RouteValues["id"]);
            Assert.Equal(OrderViewMessage.CannotMarkAsPaid, redirect.RouteValues["message"]);
        }
        [Fact]
        public async Task MarkAsPaid_ShouldChangeOrderStateToPaid()
        {
            // arrange
            var order = _db.Orders.Single(t => t.AllegroDealId == 4);
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.MarkAsPaid(order.Id);

            // assert

            order = _db.Orders.Single(t => t.AllegroDealId == 4);
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Order", redirect.ActionName);
            Assert.Equal(order.Id, redirect.RouteValues["id"]);
            Assert.Equal(OrderViewMessage.OrderMarkedAsPaid, redirect.RouteValues["message"]);
        }
        [Fact]
        public async Task DeleteCode_ShouldRedirectToIndex_NotOurAd()
        {
            // arrange
            PopulateHttpContext(UserId2);
            GameCode code = _db.Auctions.Include(t => t.GameCodes).First(t => t.AllegroAuctionId == 111).GameCodes.First();

            // act
            IActionResult result = await _controller.DeleteCode(code.Id);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", ((RedirectToActionResult)result).ActionName);
        }
        [Fact]
        public async Task DeleteCode_ShouldRedirectToIndex_CodeAlreadySold()
        {
            // arrange
            PopulateHttpContext(UserId);
            GameCode code = _db.Auctions.Include(t => t.GameCodes).ThenInclude(t => t.Order).First(t => t.AllegroAuctionId == 111).GameCodes.First(t => t.OrderId != null);

            // act
            IActionResult result = await _controller.DeleteCode(code.Id);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Codes", redirect.ActionName);
            Assert.Equal(code.AuctionId, redirect.RouteValues["id"]);
            Assert.Equal(CodeViewMessage.ErrorCodeSold, redirect.RouteValues["message"]);
        }
        [Fact]
        public async Task GetExtraCode_ShouldRedirectToIndex_NoCodesAvailable()
        {
            // arrange
            PopulateHttpContext(UserId);
            var order = _db.Orders.FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);
            var codes = _db.GameCodes.Where(t => t.Auction.AllegroAuctionId == 111 && t.Order == null);
            foreach (GameCode code in codes)
            {
                order.GameCodes.Add(code);
            }
            _db.SaveChanges();

            // act
            IActionResult result = await _controller.GetExtraCode(order.Id);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult) result);
            Assert.Equal("Order", redirect.ActionName);
            Assert.Equal(order.Id, redirect.RouteValues["id"]);
            Assert.Equal(OrderViewMessage.GenerateCodeNoCodesAvailable, redirect.RouteValues["message"]);
        }
        [Fact]
        public async Task GetExtraCode_ShouldRedirectToIndex_NotOurAd()
        {
            // arrange
            PopulateHttpContext(UserId2);
            var order = _db.Orders.FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);

            // act
            IActionResult result = await _controller.GetExtraCode(order.Id);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Index", redirect.ActionName);
        }
        [Fact]
        public async Task GetExtraCode_ShouldGenerateAdditionalCode()
        {
            // arrange
            PopulateHttpContext(UserId);
            var order = _db.Orders.FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);

            // act
            IActionResult result = await _controller.GetExtraCode(order.Id);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Order", redirect.ActionName);
            Assert.Equal(order.Id, redirect.RouteValues["id"]);
            Assert.Equal(OrderViewMessage.GenerateCodeSuccess, redirect.RouteValues["message"]);
            order = _db.Orders.Include(t => t.GameCodes).FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);

            Assert.Equal(2, order.GameCodes.Count);
            Assert.Equal("yyy", order.GameCodes.ElementAt(0).Code);
            Assert.Equal("xxx", order.GameCodes.ElementAt(1).Code);
        }
        [Fact]
        public async Task CancelOrder_ShouldRedirectToIndex_NotOurAd()
        {
            // arrange
            PopulateHttpContext(UserId2);
            var order = _db.Orders.FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);

            // act
            IActionResult result = await _controller.CancelOrder(order.Id, 1);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal(0, _allegroService.ReceivedCalls().Count());
        }
        [Fact]
        public async Task CancelOrder_ShouldRedirectToIndex_InvalidReasonId()
        {
            // arrange
            PopulateHttpContext(UserId2);
            var order = _db.Orders.FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);

            // act
            IActionResult result = await _controller.CancelOrder(order.Id, 1552);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal(0, _allegroService.ReceivedCalls().Count());
        }
        [Fact]
        public async Task CancelOrder_ShouldReleaseAllCodesAndChangeOrderStatusAndAssignRefundId()
        {
            // arrange
            PopulateHttpContext(UserId);
            var order = _db.Orders.FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);
            var codes = _db.GameCodes.Where(t => t.Auction.AllegroAuctionId == 111 && t.Order == null);
            foreach (GameCode code in codes)
            {
                order.GameCodes.Add(code);
            }
            _db.SaveChanges();
            _allegroService.SendRefund(Arg.Is<Order>(t => t.AllegroDealId == order.AllegroDealId), 1).Returns(55512);

            // act
            IActionResult result = await _controller.CancelOrder(order.Id, 1);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Order", redirect.ActionName);
            Assert.Equal(order.Id, redirect.RouteValues["id"]);
            Assert.Equal(OrderViewMessage.OrderCancelSuccess, redirect.RouteValues["message"]);

            order = _db.Orders.Include(t => t.GameCodes).FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);
            Assert.Equal(0, order.GameCodes.Count);
            Assert.Equal(OrderStatus.Canceled, order.OrderStatus);
            Assert.Equal(55512, order.AllegroRefundId);
            await _allegroService.Received(1).SendRefund(Arg.Is<Order>(t => t.AllegroDealId == order.AllegroDealId), 1);
        }
        [Fact]
        public async Task CancelOrder_ShouldRedirectToOrder_CannotCancelAlreadyCanceledOrder()
        {
            // arrange
            PopulateHttpContext(UserId);
            var order = _db.Orders.FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);
            order.OrderStatus = OrderStatus.Canceled;
            _db.SaveChanges();

            // act
            IActionResult result = await _controller.CancelOrder(order.Id, 1);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Order", redirect.ActionName);
            Assert.Equal(order.Id, redirect.RouteValues["id"]);
            Assert.Equal(OrderViewMessage.OrderCancelFail, redirect.RouteValues["message"]);

            order = _db.Orders.Include(t => t.GameCodes).FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);
            Assert.Equal(1, order.GameCodes.Count);
            Assert.Equal(OrderStatus.Canceled, order.OrderStatus);
            Assert.Equal(0, _allegroService.ReceivedCalls().Count());
        }
        [Fact]
        public async Task CancelOrder_ShouldRedirectToOrder_AllegroThrowsException()
        {
            // arrange
            PopulateHttpContext(UserId);
            var order = _db.Orders.FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);
            _allegroService.SendRefund(Arg.Is<Order>(t => t.AllegroDealId == order.AllegroDealId), 1).Throws(new Exception());

            // act
            IActionResult result = await _controller.CancelOrder(order.Id, 1);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Order", redirect.ActionName);
            Assert.Equal(order.Id, redirect.RouteValues["id"]);
            Assert.Equal(OrderViewMessage.SendingRefundFailed, redirect.RouteValues["message"]);

            order = _db.Orders.Include(t => t.GameCodes).FirstOrDefault(t => t.Auction.AllegroAuctionId == 111);
            Assert.Equal(1, order.GameCodes.Count);
            Assert.Equal(OrderStatus.Send, order.OrderStatus);
            await _allegroService.Received(1).SendRefund(Arg.Is<Order>(t => t.AllegroDealId == order.AllegroDealId), 1);
        }
        [Fact]
        public async Task DeleteCode_DeleteCodeSuccessfully()
        {
            // arrange
            PopulateHttpContext(UserId);
            GameCode code = _db.Auctions.Include(t => t.GameCodes).First(t => t.AllegroAuctionId == 111).GameCodes.First(t => t.OrderId == null);

            // act
            IActionResult result = await _controller.DeleteCode(code.Id);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult) result);
            Assert.Equal("Codes", redirect.ActionName);
            Assert.Equal(code.AuctionId, redirect.RouteValues["id"]);
            Assert.Equal(CodeViewMessage.SuccessDelete, redirect.RouteValues["message"]);
            code = _db.GameCodes.FirstOrDefault(t => t.Id == code.Id);
            Assert.Null(code);
        }
        [Fact]
        public async Task AddCodes_NotOurAd_ShouldRedirectToIndex()
        {
            // arrange
            PopulateHttpContext(UserId2);

            // act
            var result = await _controller.AddCodes(1);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Index", redirect.ActionName);
        }
        [Fact]
        public async Task AddCodes_ShouldReturnCorrectView()
        {
            // arrange
            PopulateHttpContext(UserId);

            // act
            var result = await _controller.AddCodes(1);

            // assert
            Assert.IsType<ViewResult>(result);
            var model = (AddCodesViewModel)((ViewResult)result).Model;
            Assert.Equal("test ad", model.Title);
            Assert.Equal(1, model.AuctionId);
        }
        [Fact]
        public async Task AddCodes_NoCodesSpecified_RedirectToIndex()
        {
            // arrange
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.AddCodes(new AddCodesViewModel
            {
                Codes = string.Empty,
                AuctionId = 1
            });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Index", redirect.ActionName);
        }
        [Fact]
        public async Task AddCodes_NotOurAd_RedirectToIndex()
        {
            // arrange
            PopulateHttpContext(UserId2);

            // act
            IActionResult result = await _controller.AddCodes(new AddCodesViewModel
            {
                Codes = "code",
                AuctionId = 1
            });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Index", redirect.ActionName);
        }
        [Fact]
        public async Task AddCodes_ShouldAddCodesSuccessfully()
        {
            // arrange
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.AddCodes(new AddCodesViewModel
            {
                Codes = " code\r\ncode2 \r\n",
                AuctionId = 1
            });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = ((RedirectToActionResult)result);
            Assert.Equal("Auction", redirect.ActionName);
            Assert.Equal(1, redirect.RouteValues["id"]);
            Assert.Equal(AuctionMessageId.SuccessAddCodes, redirect.RouteValues["message"]);
            var codes = _db.GameCodes.Where(t => t.AuctionId == 1 && t.Order == null).ToList();
            Assert.Equal(3, codes.Count);
            Assert.Equal(1, codes[0].AuctionId);
            Assert.Equal("yyy", codes[0].Code);
            Assert.Equal(1, codes[1].AuctionId);
            Assert.Equal("code", codes[1].Code);
            Assert.Equal(1, codes[2].AuctionId);
            Assert.Equal("code2", codes[2].Code);
        }
        [Fact]
        public async Task Auction_ShouldRefreshFees_ForExistingAuction1()
        {
            // arrange
            PopulateHttpContext(UserId);
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
            IActionResult result = await _controller.Auction(1, null, true, null, false);

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
            await TestForModelError(async () => await _controller.Auction(0, null, false, null, false));

        }
        [Fact]
        public async Task DeleteCode_ShouldRedirectToIndex_ForModelError()
        {
            await TestForModelError(() => _controller.DeleteCode(1));
        }
        [Fact]
        public async Task AddCodes_ShouldRedirectToIndex_ForModelError()
        {
            await TestForModelError(() => _controller.AddCodes(new AddCodesViewModel()));
        }
        [Fact]
        public async Task AuctionPost_ShouldRedirectToIndex_ForModelError()
        {
            await TestForModelError(() => _controller.Auction(null));
        }
        [Fact]
        public async Task AuctionPost_ShouldRedirectToIndex_ForNotExistingAuction()
        {
            // arrange
            PopulateHttpContext(UserId2);

            // act
            IActionResult result = await _controller.Auction(new AuctionViewModel { Id = 6});

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }

        [Fact]
        public async Task AuctionPost_ShouldRedirectToIndex_ForAuctionThatBelongsToDifferentUser()
        {
            // arrange
            PopulateHttpContext(UserId2);

            // act
            IActionResult result = await _controller.Auction(new AuctionViewModel { Id = 1 });

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
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.Auction(new AuctionViewModel { Id = 1, IsMonitored = false, IsVirtualItem = true });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult) result;
            Auction auction = _db.Auctions.Single(t => t.Id == 1);

            Assert.False(auction.IsMonitored);
            Assert.Equal("Auction", redirect.ActionName);
            Assert.Equal(1, redirect.RouteValues["id"]);
            Assert.Equal(true, redirect.RouteValues["settingsTabActive"]);
            Assert.Equal(AuctionMessageId.SuccessSaveSettings, redirect.RouteValues["message"]);
        }
        [Fact]
        public async Task AuctionPost_ShouldDisableAutomaticRefundsAndFeedbackForAuction1()
        {
            // arrange
            PopulateHttpContext(UserId);
            var ad = _db.Auctions.First(t => t.UserId == UserId);
            ad.AutomaticRefundsEnabled = ad.AutomaticFeedbackEnabled = true;
            _db.SaveChanges();

            // act
            IActionResult result = await _controller.Auction(new AuctionViewModel { Id = ad.Id, AutomaticRefundsEnabled = false, AutomaticFeedbackEnabled = false });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Auction auction = _db.Auctions.Single(t => t.Id == ad.Id);

            Assert.False(auction.AutomaticFeedbackEnabled);
            Assert.False(auction.AutomaticRefundsEnabled);
            Assert.Equal("Auction", redirect.ActionName);
            Assert.Equal(ad.Id, redirect.RouteValues["id"]);
            Assert.Equal(true, redirect.RouteValues["settingsTabActive"]);
            Assert.Equal(AuctionMessageId.SuccessSaveSettings, redirect.RouteValues["message"]);
        }
        [Fact]
        public async Task AuctionPost_ShouldEnableAutomaticRefundsForAuction1()
        {
            // arrange
            PopulateHttpContext(UserId);
            var ad = _db.Auctions.First(t => t.UserId == UserId);

            // act
            IActionResult result = await _controller.Auction(new AuctionViewModel { Id = ad.Id, AutomaticRefundsEnabled = true, AutomaticFeedbackEnabled = false });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Auction auction = _db.Auctions.Single(t => t.Id == ad.Id);

            Assert.False(auction.AutomaticFeedbackEnabled);
            Assert.True(auction.AutomaticRefundsEnabled);
            Assert.Equal("Auction", redirect.ActionName);
            Assert.Equal(ad.Id, redirect.RouteValues["id"]);
            Assert.Equal(true, redirect.RouteValues["settingsTabActive"]);
            Assert.Equal(AuctionMessageId.SuccessSaveSettings, redirect.RouteValues["message"]);
        }
        [Fact]
        public async Task AuctionPost_ShouldEnableAutomaticFeedbackForAuction1()
        {
            // arrange
            PopulateHttpContext(UserId);
            var ad = _db.Auctions.First(t => t.UserId == UserId);

            // act
            IActionResult result = await _controller.Auction(new AuctionViewModel { Id = ad.Id, AutomaticRefundsEnabled = false, AutomaticFeedbackEnabled = true });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Auction auction = _db.Auctions.Single(t => t.Id == ad.Id);

            Assert.True(auction.AutomaticFeedbackEnabled);
            Assert.False(auction.AutomaticRefundsEnabled);
            Assert.Equal("Auction", redirect.ActionName);
            Assert.Equal(ad.Id, redirect.RouteValues["id"]);
            Assert.Equal(true, redirect.RouteValues["settingsTabActive"]);
            Assert.Equal(AuctionMessageId.SuccessSaveSettings, redirect.RouteValues["message"]);
        }
        [Fact]
        public async Task AuctionPost_ShouldEnableMonitoringAndDisableVirtualItem_ForAuction2()
        {
            // arrange
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.Auction(new AuctionViewModel { Id = 2, IsMonitored = true, IsVirtualItem = false });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult) result;
            Auction auction = _db.Auctions.Single(t => t.Id == 2);

            Assert.False(auction.IsVirtualItem);
            Assert.True(auction.IsMonitored);
            Assert.Equal("Auction", redirect.ActionName);
            Assert.Equal(2, redirect.RouteValues["id"]);
            Assert.Equal(true, redirect.RouteValues["settingsTabActive"]);
            Assert.Equal(AuctionMessageId.SuccessSaveSettings, redirect.RouteValues["message"]);
        }

        [Fact]
        public async Task AuctionPost_ShouldNotAllowToSelectVirtualItem_ForAuction3()
        {
            // arrange
            PopulateHttpContext(UserId2);
            Auction ad = _db.Auctions.First(t => t.AllegroAuctionId == 333);

            // act
            IActionResult result = await _controller.Auction(new AuctionViewModel { Id = ad.Id, IsMonitored = true, IsVirtualItem = true });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Auction auction = _db.Auctions.Single(t => t.Id == ad.Id);

            Assert.False(auction.IsVirtualItem);
            Assert.True(auction.IsMonitored);
            Assert.Equal("Auction", redirect.ActionName);
            Assert.Equal(ad.Id, redirect.RouteValues["id"]);
            Assert.Equal(AuctionMessageId.CannotSetVirtualItem, redirect.RouteValues["message"]);
            Assert.Equal(true, redirect.RouteValues["settingsTabActive"]);
        }

        [Fact]
        public async Task AuctionPost_ShouldNotChangeMonitoringSetting_ForAuction4()
        {
            // arrange
            PopulateHttpContext(UserId2);

            // act
            IActionResult result = await _controller.Auction(new AuctionViewModel { Id = 4, IsMonitored = false, IsVirtualItem = false });

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult) result;
            Auction auction = _db.Auctions.Single(t => t.Id == 4);

            Assert.False(auction.IsMonitored);
            Assert.Equal("Auction", redirect.ActionName);
            Assert.Equal(4, redirect.RouteValues["id"]);
            Assert.Equal(true, redirect.RouteValues["settingsTabActive"]);
            Assert.Equal(AuctionMessageId.SuccessSaveSettings, redirect.RouteValues["message"]);
        }

        [Fact]
        public async Task Order_ShouldRedirectToIndex_ForModelError()
        {
            await TestForModelError(() => _controller.Auction(new AuctionViewModel {Id = 4, IsMonitored = false, IsVirtualItem = false}));
        }

        [Fact]
        public async Task Order_ShouldRedirectToIndex_ForNotExistingOrder()
        {
            // arrange
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.Order(-1);

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
            PopulateHttpContext(UserId);
            int orderId = _db.Orders.First(t => t.Auction.UserId == UserId2).Id;

            // act
            IActionResult result = await _controller.Order(orderId);

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
            PopulateHttpContext(UserId);
            int orderId = _db.Orders.First(t => t.AllegroDealId == 2).Id;

            // act
            IActionResult result = await _controller.Order(orderId);

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
            PopulateHttpContext(UserId);
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
            IActionResult result = await _controller.Add(true);

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
            await TestForModelError(() => _controller.Add(new AddViewModel()));
        }
        [Fact]
        public async Task AddPost_ShouldSaveAndUpdateFees_ForOneSelectedAuction()
        {
            // arrange
            PopulateHttpContext(UserId);
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
            IActionResult result = await _controller.Add(model);

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            var redirect = (RedirectToActionResult) result;
            Auction ad = _db.Auctions.Single(t => t.AllegroAuctionId == 1261);

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

        [Fact]
        public async Task Add_ShouldNotFetch()
        {
            // arrange
            PopulateHttpContext(UserId);

            // act
            IActionResult result = await _controller.Add(false);

            // assert
            Assert.IsType<ViewResult>(result);
            Assert.IsType<AddViewModel>(((ViewResult)result).Model);
            await _allegroService.DidNotReceiveWithAnyArgs().GetNewAuctions();
        }
        private async Task TestForModelError(Func<Task<IActionResult>> action)
        {
            // arrange
            PopulateHttpContext(UserId);
            _controller.ModelState.AddModelError("error", "some error");

            // act
            IActionResult result = await action();

            // assert
            Assert.IsType<RedirectToActionResult>(result);
            RedirectToActionResult redirect = (RedirectToActionResult)result;
            Assert.Null(redirect.ControllerName);
            Assert.Equal("Index", redirect.ActionName);
        }
        private void PopulateHttpContext(string userId)
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var httpContext = _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
            _controller.ControllerContext.HttpContext = httpContext;
        }

        protected override void CreateFakeData()
        {
            base.CreateFakeData();
            using (var scope = CreateScope())
            {
                var database = GetDatabase(scope);
                var ad = database.Auctions.Include(t => t.Orders).First(t => t.AllegroAuctionId == 111);
                ad.GameCodes.Add(new GameCode
                {
                    Code = "xxx",
                    AddDate = new DateTime(2012, 5, 4),
                    Order = ad.Orders.First()
                });
                ad.GameCodes.Add(new GameCode
                {
                    Code = "yyy",
                    AddDate = new DateTime(2012, 5, 5)
                });

                ad = database.Auctions.First(t => t.AllegroAuctionId == 7731);
                ad.GameCodes.Add(new GameCode
                {
                    Code = "aaa",
                    AddDate = new DateTime(2012, 5, 6)
                });
                ad.GameCodes.Add(new GameCode
                {
                    Code = "bbb",
                    AddDate = new DateTime(2012, 5, 7)
                });

                database.SaveChanges();
            }
        }
    }
}
