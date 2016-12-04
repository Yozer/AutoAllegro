using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using AutoAllegro.Data;
using AutoAllegro.Helpers.Extensions;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Services.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Packaging;

namespace AutoAllegro.Controllers
{
    [Authorize]
    public class AuctionController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IAllegroService _allegroService;
        private readonly UserManager<User> _userManager;
        private readonly IMapper _mapper;

        public AuctionController(ApplicationDbContext dbContext, UserManager<User> userManager, IAllegroService allegroService, IMapper mapper)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _allegroService = allegroService;
            _mapper = mapper;
        }
        public async Task<IActionResult> Index(int? page)
        {
            const int pageSize = 15;
            var auctions = await GetUserAuctions();
            if (auctions == null)
            {
                return RedirectToAction(nameof(AccountController.Login), "Account");
            }

            var viewModel = new IndexViewModel
            {
                Auctions = _mapper.Map<List<AuctionViewModel>>(auctions)
            };

            viewModel.Paginate(page, pageSize, c => c.Auctions);
            return View(viewModel);
        }


        public async Task<IActionResult> Auction(int id, int? page, bool refresh)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Index));
            }

            const int pageSize = 25;
            var auction = await GetUserAuctionWithOrders(id);
            if (auction == null)
                return RedirectToAction(nameof(Index));

            if (refresh)
            {
                await _allegroService.Login(auction.User.AllegroUserName, auction.User.AllegroHashedPass, auction.User.AllegroKey);
                await _allegroService.UpdateAuctionFees(auction);
                await _dbContext.SaveChangesAsync();
            }

            // possible bottleneck
            var viewModel = _mapper.Map<AuctionViewModel>(auction);
            viewModel.Paginate(page, pageSize, c => c.Orders);
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Auction(Auction updatedAuction)
        {
            if (!ModelState.IsValid)
                return RedirectToAction(nameof(Index));

            var auction = await GetUserAuction(updatedAuction.Id);
            if (auction == null)
                return RedirectToAction(nameof(Index));

            auction.IsMonitored = updatedAuction.IsMonitored;

            await _dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Auction), new {id = auction.Id});
        }

        public async Task<IActionResult> Order(int id)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Index));
            }

            var order = await GetUserOrder(id);
            if (order == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var viewModel = _mapper.Map<OrderViewModel>(order);
            return View(viewModel);
        }
        public async Task<IActionResult> Add(bool? fetch)
        {
            AddViewModel model = new AddViewModel();

            if (fetch.GetValueOrDefault())
            {

                var user = await _userManager.GetUserAsync(User);
                await _allegroService.Login(user.AllegroUserName, user.AllegroHashedPass, user.AllegroKey);
                ImmutableHashSet<long> userAuctions = _dbContext.Auctions.Where(t => t.UserId == user.Id).Select(t => t.AllegroAuctionId).ToImmutableHashSet();
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

            var user = await _userManager.GetUserAsync(User);
            await _allegroService.Login(user.AllegroUserName, user.AllegroHashedPass, user.AllegroKey);
            var auctions = model.Auctions
                .Where(t => t.IsMonitored)
                .Select(t => new Auction
                {
                    AllegroAuctionId = t.Id,
                    Converter = 1,
                    CreationDate = t.StartDate,
                    EndDate = t.EndDate,
                    IsMonitored = true,
                    PricePerItem = t.Price,
                    Title = t.Name
                })
                .Select(_allegroService.UpdateAuctionFees).ToList();

            await Task.WhenAll(auctions);
            user.Auctions.AddRange(auctions.Select(t => t.Result));

            await _dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private Task<string> GetUserId()
        {
            return _userManager.GetUserAsync(HttpContext.User).ContinueWith(t => t.Result?.Id);
        }
        private Task<List<Auction>> GetUserAuctions()
        {
            return GetUserId()
                .ContinueWith(task => _dbContext.Auctions.Where(t => t.User.Id == task.Result).ToList());
        }
        private Task<Auction> GetUserAuctionWithOrders(int id)
        {
            return GetUserId()
                    .ContinueWith(task => _dbContext.Auctions.Include(t => t.Orders).FirstOrDefault(t => t.User.Id == task.Result && t.Id == id));
        }

        private Task<Auction> GetUserAuction(int id)
        {
            return GetUserId()
                .ContinueWith(task => _dbContext.Auctions.FirstOrDefault(t => t.Id == id && t.User.Id == task.Result));
        }

        private Task<Order> GetUserOrder(int id)
        {
            throw new NotImplementedException();
            return GetUserId()
                .ContinueWith(task => _dbContext.Users
                    .Include(t => t.Auctions).Where(t => t.Id == task.Result)
                    .SelectMany(t => t.Auctions).Include(t => t.Orders)
                    .SelectMany(t => t.Orders).Include(t => t.ShippingAddress)
                    .FirstOrDefault(t => t.Id == id));
        }
    }
}
