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
            Auction auction = await (
                from ad in _dbContext.Auctions.Include(t => t.Orders).ThenInclude(t => t.Buyer)
                where ad.UserId == GetUserId() && ad.Id == id
                select ad).FirstOrDefaultAsync();

            if (auction == null)
                return RedirectToAction(nameof(Index));

            int codesCount = await _dbContext.GameCodes.CountAsync(t => t.AuctionId == auction.Id && t.Order == null);

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
            viewModel.FreeCodesCount = codesCount;
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
            return RedirectToAction(nameof(Auction), new {id = auction.Id, settingsTabActive = true, message = AuctionMessageId.SuccessSaveSettings});
        }

        public async Task<IActionResult> Order(int id)
        {
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

        public async Task<IActionResult> Codes(int id, int? page, CodeViewMessage? message)
        {
            const int pageSize = 25;
            var auction = await (from ad in _dbContext.Auctions.Include(t => t.GameCodes)
                                where ad.UserId == GetUserId() && ad.Id == id && ad.IsVirtualItem
                                select ad).FirstOrDefaultAsync();

            if (auction == null)
                return RedirectToAction(nameof(Index));

            var model = new CodesViewModel
            {
                AuctionId = id,
                Title = auction.Title,
                Codes = _mapper.Map<IList<CodeViewModel>>(auction.GameCodes.Where(t => t.OrderId == null).OrderByDescending(t => t.AddDate).ThenBy(t => t.Id)),
                Message = message
            };
            model.Paginate(page, pageSize, t => t.Codes);

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCode(int id)
        {
            if(!ModelState.IsValid)
                return RedirectToAction(nameof(Index));

            var code = await _dbContext.GameCodes.Include(t => t.Auction).FirstOrDefaultAsync(t => t.Id == id);
            if (code?.Auction?.UserId != GetUserId())
                return RedirectToAction(nameof(Index));

            if(code.OrderId != null)
                return RedirectToAction(nameof(Codes), new { id = code.AuctionId, message = CodeViewMessage.ErrorCodeSold });

            _dbContext.Entry(code).State = EntityState.Deleted;
            await _dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Codes), new { id = code.AuctionId, message = CodeViewMessage.SuccessDelete });
        }

        public async Task<IActionResult> AddCodes(int id)
        {
            var auctionTitle = await (from ad in _dbContext.Auctions
                                      where id == ad.Id && ad.UserId == GetUserId() && ad.IsVirtualItem
                                      select ad.Title).FirstOrDefaultAsync();
            if(auctionTitle == null)
                return RedirectToAction(nameof(Index));

            var model = new AddCodesViewModel
            {
                AuctionId = id,
                Title = auctionTitle
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCodes(AddCodesViewModel model)
        {
            if (!ModelState.IsValid)
                return RedirectToAction(nameof(Index));

            var codes = model.Codes.Split(new[] {"\n"}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => new GameCode
                {
                    Code = t.Trim(),
                    AddDate = DateTime.Now
                }).ToList();

            if (codes.Count == 0)
                return RedirectToAction(nameof(Index));

            var auction = await (from ad in _dbContext.Auctions
                                where model.AuctionId == ad.Id && ad.UserId == GetUserId() && ad.IsVirtualItem
                                select ad).FirstOrDefaultAsync();

            if (auction == null)
                return RedirectToAction(nameof(Index));
            auction.GameCodes.AddRange(codes);
            await _dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Auction), new { id = model.AuctionId, message = AuctionMessageId.SuccessAddCodes });
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
        private Task<Auction> GetUserAuction(int id)
        {
            return (from auction in _dbContext.Auctions.Include(t => t.User)
                    where auction.Id == id && auction.UserId == GetUserId()
                    select auction).FirstOrDefaultAsync();
        }

        private Task<Order> GetUserOrder(int id)
        {
            return (from order in _dbContext.Orders.Include(t => t.Buyer).Include(t => t.ShippingAddress).Include(t => t.Auction).Include(t => t.GameCodes)
                    where order.Id == id && order.Auction.User.Id == GetUserId()
                    select order).FirstOrDefaultAsync();
        }
    }


}
