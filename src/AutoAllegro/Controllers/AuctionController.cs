using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using AutoAllegro.Data;
using AutoAllegro.Helpers.Extensions;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Services;
using AutoAllegro.Services.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoAllegro.Controllers
{
    [Authorize]
    public class AuctionController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IAllegroService _allegroService;
        private readonly UserManager<User> _userManager;
        private readonly IMapper _mapper;
        private readonly IAllegroProcessor _allegroProcessor;

        public AuctionController(ApplicationDbContext dbContext, UserManager<User> userManager, IAllegroService allegroService, IMapper mapper, IAllegroProcessor allegroProcessor)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _allegroService = allegroService;
            _mapper = mapper;
            _allegroProcessor = allegroProcessor;
        }
        public async Task<IActionResult> Index(int? page)
        {
            const int pageSize = 15;
            var auctions = await GetUserAuctions();

            var viewModel = new IndexViewModel
            {
                Auctions = _mapper.Map<List<AuctionViewModel>>(auctions)
            };

            viewModel.Paginate(page, pageSize, c => c.Auctions);
            return View(viewModel);
        }


        public async Task<IActionResult> Auction(int id, int? page, bool refresh, AuctionMessageId? message, bool settingsTabActive)
        {
            if (!ModelState.IsValid)
                return RedirectToAction(nameof(Index));

            const int pageSize = 25;
            var auction = await GetUserAuctionWithOrders(id);
            if (auction == null)
                return RedirectToAction(nameof(Index));

            if (refresh)
            {
                await LoginToAllegro();
                await _allegroService.UpdateAuctionFees(auction);
                await _dbContext.SaveChangesAsync();
            }

            // possible bottleneck
            var viewModel = _mapper.Map<AuctionViewModel>(auction);
            viewModel.Message = message;
            viewModel.SettingsTabActive = settingsTabActive;
            viewModel.Paginate(page, pageSize, c => c.Orders);
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Auction(AuctionViewModel updatedAuction)
        {
            if (!ModelState.IsValid)
                return RedirectToAction(nameof(Index));

            var auction = await GetUserAuction(updatedAuction.Id);
            if (auction == null)
                return RedirectToAction(nameof(Index));
            else if (updatedAuction.IsVirtualItem && auction.User.VirtualItemSettingsId == null)
                return RedirectToAction(nameof(Auction), new { id = auction.Id, message = AuctionMessageId.CannotSetVirtualItem, settingsTabActive = true });

            if (updatedAuction.IsMonitored != auction.IsMonitored)
            {
                auction.IsMonitored = updatedAuction.IsMonitored;
                if (updatedAuction.IsMonitored)
                    _allegroProcessor.StartProcessor(auction);
                else
                    _allegroProcessor.StopProcessor(auction);
            }
            auction.IsVirtualItem = updatedAuction.IsVirtualItem;

            await _dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Auction), new {id = auction.Id, settingsTabActive = true, message = AuctionMessageId.Success});
        }

        public async Task<IActionResult> Order(int id)
        {
            if (!ModelState.IsValid)
                return RedirectToAction(nameof(Index));

            var order = await GetUserOrder(id);
            if (order == null)
                return RedirectToAction(nameof(Index));

            var viewModel = _mapper.Map<OrderViewModel>(order);
            return View(viewModel);
        }
        public async Task<IActionResult> Add(bool? fetch)
        {
            AddViewModel model = new AddViewModel();

            if (fetch.GetValueOrDefault())
            {
                await LoginToAllegro();
                ImmutableHashSet<long> userAuctions = _dbContext.Auctions.Where(t => t.UserId == GetUserId()).Select(t => t.AllegroAuctionId).ToImmutableHashSet();
                model.Auctions = (await _allegroService.GetNewAuctions()).Where(t => !userAuctions.Contains(t.Id)).ToList();
            }

            return View(model);
        }

        // POST: /Auction/Add
        [HttpPost]
        public async Task<IActionResult> Add(AddViewModel model)
        {
            if(!ModelState.IsValid)
                return RedirectToAction(nameof(Index));

            await LoginToAllegro();
            var auctions = model.Auctions
                .Where(t => t.ShouldBeSaved)
                .Select(t => new Auction
                {
                    AllegroAuctionId = t.Id,
                    Converter = 1,
                    CreationDate = t.StartDate,
                    EndDate = t.EndDate,
                    PricePerItem = t.Price,
                    Title = t.Name,
                    UserId = GetUserId()
                })
                .Select(_allegroService.UpdateAuctionFees).ToList();

            await Task.WhenAll(auctions);
            _dbContext.Auctions.AddRange(auctions.Select(t => t.Result));

            await _dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task LoginToAllegro()
        {
            if (_allegroService.IsLoginRequired(GetUserId()))
            {
                var user = await _userManager.GetUserAsync(User);
                var allegroCredentials = new AllegroCredentials(user.AllegroUserName, user.AllegroHashedPass, user.AllegroKey, user.AllegroJournalStart);
                await _allegroService.Login(GetUserId(), allegroCredentials);
            }
        }

        private string GetUserId()
        {
            return _userManager.GetUserId(User);
        }
        private Task<List<Auction>> GetUserAuctions()
        {
            return (from auction in _dbContext.Auctions
                    where auction.UserId == GetUserId()
                    select auction).ToListAsync();
        }
        private Task<Auction> GetUserAuctionWithOrders(int id)
        {
            return (from auction in _dbContext.Auctions.Include(t => t.Orders).ThenInclude(t => t.Buyer)
                    where auction.UserId == GetUserId() && auction.Id == id
                    select auction).FirstOrDefaultAsync();
        }

        private Task<Auction> GetUserAuction(int id)
        {
            return (from auction in _dbContext.Auctions.Include(t => t.User)
                    where auction.Id == id && auction.UserId == GetUserId()
                    select auction).FirstOrDefaultAsync();
        }

        private Task<Order> GetUserOrder(int id)
        {
            return (from order in _dbContext.Orders.Include(t => t.Buyer).Include(t => t.ShippingAddress).Include(t => t.Auction)
                    where order.Id == id && order.Auction.User.Id == GetUserId()
                    select order).FirstOrDefaultAsync();
        }
    }
}
