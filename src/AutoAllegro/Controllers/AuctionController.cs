using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Models.HelperModels;
using AutoAllegro.Services;
using AutoAllegro.Services.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoaAllegroService;

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

            page = page ?? 1;
            --page;
            int from = Math.Max(0, page.Value * pageSize);
            var view = new IndexViewModel
            {
                PaginationSettings = new PaginationSettings
                {
                    CurrentPage = page.Value + 1,
                    PagesCount = Math.Max(1, (int) Math.Ceiling(auctions.Count/(decimal) pageSize))
                },
                Auctions = _mapper.Map<List<AuctionViewModel>>(auctions.Skip(from).Take(pageSize).ToList())
            };

            return View(view);
        }


        public async Task<IActionResult> Auction(int id, int? page)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Index));
            }

            const int pageSize = 25;
            var auction = await GetUserAuction(id);
            if (auction == null)
            {
                return RedirectToAction(nameof(Index));
            }


            page = page ?? 1;
            --page;
            int from = Math.Max(0, page.Value * pageSize);

            auction.Orders = auction.Orders.Skip(from).Take(pageSize).ToList();
            var viewModel = _mapper.Map<AuctionViewModel>(auction);
            viewModel.PaginationSettings = new PaginationSettings
            {
                CurrentPage = page.Value + 1,
                PagesCount = Math.Max(1, (int)Math.Ceiling(auction.Orders.Count / (decimal)pageSize))
            };

            return View(viewModel);
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
        public IActionResult Add(AddViewModel model)
        {
            return RedirectToAction(nameof(Index));
        }

        private Task<string> GetUserId()
        {
            return _userManager.GetUserAsync(HttpContext.User).ContinueWith(t => t.Result?.Id);
        }
        private Task<ICollection<Auction>> GetUserAuctions()
        {
            return GetUserId()
                .ContinueWith(task => _dbContext.Users.Include(t => t.Auctions).Where(t => t.Id == task.Result).SingleOrDefaultAsync()
                .ContinueWith(t => t.Result?.Auctions))
                .Unwrap();
        }
        private Task<Auction> GetUserAuction(int id)
        {
            return GetUserId()
                .ContinueWith(task => _dbContext.Users.Include(t => t.Auctions)
                .Where(t => t.Id == task.Result).SelectMany(t => t.Auctions)
                .Where(t => t.Id == id).Include(t => t.Orders)
                .SingleOrDefaultAsync())
                .Unwrap();
        }

        private Task<Order> GetUserOrder(int id)
        {
            return GetUserId()
                .ContinueWith(task => _dbContext.Users.Include(t => t.Auctions)
                    .Where(t => t.Id == task.Result).SelectMany(t => t.Auctions)
                    .Include(t => t.Orders).SelectMany(t => t.Orders)
                    .Include(t => t.ShippingAddress).Where(t => t.Id == id)
                    .SingleOrDefaultAsync())
                .Unwrap();
        }
    }
}
