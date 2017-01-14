using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
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
        private const string EmailPattern = "^([0-9a-zA-Z]([-\\.\\w]*[0-9a-zA-Z])*@([0-9a-zA-Z][-\\w]*[0-9a-zA-Z]\\.)+[a-zA-Z]{2,9})$";

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

            var viewModel = new IndexViewModel
            {
                Auctions = _mapper.Map<List<AuctionViewModel>>(auctions)
            };

            viewModel.Paginate(page, pageSize, c => c.Auctions);
            return View(viewModel);
        }


        public async Task<IActionResult> Auction(int id, int? page, bool refresh, AuctionMessageId? message, bool settingsTabActive, string searchString = null)
        {
            if (!ModelState.IsValid)
                return RedirectToAction(nameof(Index));

            const int pageSize = 25;
            Auction auction = string.IsNullOrEmpty(searchString) ? 
                await (
                    from ad in _dbContext.Auctions.Include(t => t.Orders).ThenInclude(t => t.Buyer)
                    where ad.UserId == GetUserId() && ad.Id == id
                    select ad).FirstOrDefaultAsync() 
                : await (
                    from ad in _dbContext.Auctions
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
            if(!string.IsNullOrEmpty(searchString))
            {
                if (Regex.IsMatch(searchString, EmailPattern))
                {
                   var orders = await GetOrdersByEmail(id,searchString);
                    viewModel.Orders = _mapper.Map<List<OrderViewModel>>(orders);
                } 
                else 
                {
                   var orders = await GetOrdersByName(id,searchString);
                    viewModel.Orders = _mapper.Map<List<OrderViewModel>>(orders);

                }
            }

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
                return RedirectToAction(nameof(Auction), new { id = auction.Id, message = AuctionMessageId.CannotSetVirtualItem, settingsTabActive = true, searchString = updatedAuction.SearchString});

            auction.IsMonitored = updatedAuction.IsMonitored;
            auction.IsVirtualItem = updatedAuction.IsVirtualItem;
            auction.AutomaticFeedbackEnabled = updatedAuction.AutomaticFeedbackEnabled;
            auction.AutomaticRefundsEnabled = updatedAuction.AutomaticRefundsEnabled;

            await _dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Auction), new { id = auction.Id, settingsTabActive = true, message = AuctionMessageId.SuccessSaveSettings, searchString = updatedAuction.SearchString });
        }
        
        public async Task<IActionResult> Order(int id, OrderViewMessage? message = null)
        {
            var order = await GetUserOrder(id);
            if (order == null)
                return RedirectToAction(nameof(Index));

            var viewModel = _mapper.Map<OrderViewModel>(order);
            viewModel.Message = message;
            viewModel.RefundReasons = await _dbContext.AllegroRefundReasons.ToListAsync();
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
            if (!ModelState.IsValid)
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetExtraCode(int id)
        {
            var order = await _dbContext.Orders.FirstOrDefaultAsync(t => t.Id == id && t.Auction.UserId == GetUserId() && t.Auction.IsVirtualItem);
            if (order == null)
                return RedirectToAction(nameof(Index));

            var extraCode = await _dbContext.GameCodes.FirstOrDefaultAsync(t => t.AuctionId == order.AuctionId && t.Order == null);
            if(extraCode == null)
                return RedirectToAction(nameof(Order), new {id, message = OrderViewMessage.GenerateCodeNoCodesAvailable });

            order.GameCodes.Add(extraCode);
            await _dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Order), new {id, message = OrderViewMessage.GenerateCodeSuccess});

        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id, int reasonId)
        {
            var order = await _dbContext.Orders.FirstOrDefaultAsync(t => t.Id == id && t.Auction.UserId == GetUserId());
            if (order == null || !await _dbContext.AllegroRefundReasons.AnyAsync(t => t.Id == reasonId))
                return RedirectToAction(nameof(Index));

            if (order.OrderStatus != OrderStatus.Canceled)
            {

                int refundId;

                await LoginToAllegro();
                try
                {
                    refundId = await _allegroService.SendRefund(order, reasonId);
                }
                catch (Exception)
                {
                    return RedirectToAction(nameof(Order), new { id, message = OrderViewMessage.SendingRefundFailed });
                }

                order.AllegroRefundId = refundId;
                order.OrderStatus = OrderStatus.Canceled;

                await _dbContext.SaveChangesAsync();
                return RedirectToAction(nameof(Order), new {id, message = OrderViewMessage.OrderCancelSuccess});
            }
            else
            {
                return RedirectToAction(nameof(Order), new { id, message = OrderViewMessage.OrderCancelFail });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FreeCodes(int id)
        {
            var order = await _dbContext.Orders.Include(t => t.GameCodes).FirstOrDefaultAsync(t => t.Id == id && t.Auction.UserId == GetUserId());
            if (order == null)
                return RedirectToAction(nameof(Index));

            if (order.OrderStatus == OrderStatus.Canceled)
            {
                foreach (var code in order.GameCodes)
                {
                    code.Order = null;
                }

                await _dbContext.SaveChangesAsync();
                return RedirectToAction(nameof(Order), new { id, message = OrderViewMessage.FreeCodesSuccess });
            }
            else
            {
                return RedirectToAction(nameof(Order), new { id, message = OrderViewMessage.FreeCodesOnlyForCanceledOrder });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var order = await _dbContext.Orders.FirstOrDefaultAsync(t => t.Id == id && t.Auction.UserId == GetUserId() && t.OrderStatus == OrderStatus.Created || t.OrderStatus == OrderStatus.Canceled);
            if (order == null)
                return RedirectToAction(nameof(Index));

            if (order.AllegroRefundId != null)
            {
                await LoginToAllegro();
                if (!await _allegroService.CancelRefund(order.AllegroRefundId.Value))
                {
                    return RedirectToAction(nameof(Order), new { id, message = OrderViewMessage.CannotMarkAsPaid });
                }

                order.AllegroRefundId = null;
            }

            order.AllegroRefundId = null;
            order.OrderStatus = OrderStatus.Paid;
            await _dbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Order), new { id, message = OrderViewMessage.OrderMarkedAsPaid });
        }
        private async Task LoginToAllegro()
        {
            var user = await _userManager.GetUserAsync(User);
            var allegroCredentials = new AllegroCredentials(user.AllegroUserName, user.AllegroHashedPass, user.AllegroKey, null);
            await _allegroService.Login(GetUserId(), allegroCredentials);
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

        private Task<List<Order>> GetOrdersByName(int id,string userLogin)
        {
            return (from order in _dbContext.Orders.Include(t=> t.Buyer)
                where order.Auction.UserId == GetUserId() && order.AuctionId == id && order.Buyer.UserLogin.Contains(userLogin.Trim())
                select order).ToListAsync();
        }

        private Task<List<Order>> GetOrdersByEmail(int id, string userEmail)
        {
            return (from order in _dbContext.Orders.Include(t => t.Buyer)
                where order.Auction.UserId == GetUserId() && order.AuctionId == id && order.Buyer.Email.Contains(userEmail.Trim())
                select order).ToListAsync();
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
