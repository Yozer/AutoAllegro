using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Models.StatsViewModels;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IndexViewModel = AutoAllegro.Models.StatsViewModels.IndexViewModel;

namespace AutoAllegro.Controllers
{
    [Authorize]
    public class StatsController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<User> _userManager;
        private readonly IMapper _mapper;

        public StatsController(ApplicationDbContext dbContext, IMapper mapper, UserManager<User> userManager)
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new IndexViewModel();

            var query = from order in _dbContext.Orders
                where order.Auction.UserId == _userManager.GetUserId(User)
                group order by new {order.OrderDate.Year, order.OrderDate.Month}
                into g
                select new {g.Key, Value = g.Sum(t => t.Quantity * t.Auction.PricePerItem)};

            viewModel.YearlyStats = query.Select(t => new Tuple<DateTime, decimal>(new DateTime(t.Key.Year, t.Key.Month, 1), t.Value)).OrderBy(t => t.Item1).ToList();
            var auctions = await _dbContext.Auctions.Where(t => t.UserId == _userManager.GetUserId(User)).ToListAsync();
            viewModel.Auctions = _mapper.Map<List<Models.AuctionViewModels.AuctionViewModel>>(auctions);

            return View(viewModel);
        }

        public IActionResult Daily(string date) 
        {
            var viewModel = new DailyViewModel
            {
                StatsDate = date
            };

            DateTime dateToSearch = DateTime.ParseExact(date, "MMMMM yyyy", CultureInfo.CurrentCulture);

            var query = from order in _dbContext.Orders
                        where order.Auction.UserId == _userManager.GetUserId(User) && order.OrderDate.Year == dateToSearch.Year && order.OrderDate.Month == dateToSearch.Month
                        group order by new { order.OrderDate.Year, order.OrderDate.Month, order.OrderDate.Day }
                        into g
                        select new { g.Key, Value = g.Sum(t => t.Quantity * t.Auction.PricePerItem) };

            viewModel.DailyStats = query.Select(t => new Tuple<DateTime, decimal>(new DateTime(t.Key.Year, t.Key.Month, t.Key.Day), t.Value)).ToList();
            for (int day = 1; day <= DateTime.DaysInMonth(dateToSearch.Year, dateToSearch.Month); day++)
            {
                if(viewModel.DailyStats.All(t => t.Item1.Day != day))
                    viewModel.DailyStats.Add(new Tuple<DateTime, decimal>(new DateTime(dateToSearch.Year, dateToSearch.Month, day), 0));
            }

            viewModel.DailyStats = viewModel.DailyStats.OrderBy(t => t.Item1).ToList();
            return View(viewModel);
        }

        public async Task<IActionResult> Auction(int id)
        {
            var auction = await _dbContext.Auctions.Where(t => t.UserId == _userManager.GetUserId(User)).FirstOrDefaultAsync(t => t.Id == id);
            if (auction == null)
                return RedirectToAction(nameof(Index));

            var viewModel = _mapper.Map<AuctionViewModel>(auction);
            var query = from order in _dbContext.Orders
                        where order.Auction.UserId == _userManager.GetUserId(User) && order.Auction.Id == id
                        group order by new { order.OrderDate.Year, order.OrderDate.Month, order.OrderDate.Day }
                        into g
                        select new { g.Key, Value = g.Sum(t => t.Quantity) };

            viewModel.SoldItems = query.Select(t => new Tuple<DateTime, int>(new DateTime(t.Key.Year, t.Key.Month, t.Key.Day), t.Value)).OrderBy(t => t.Item1).ToList();

            for (DateTime startDate = auction.CreationDate.Date; startDate <= auction.EndDate.Date; startDate = startDate.AddDays(1))
            {
                if (viewModel.SoldItems.All(t => t.Item1.Date != startDate))
                    viewModel.SoldItems.Add(new Tuple<DateTime, int>(startDate.Date, 0));
            }
            viewModel.SoldItems = viewModel.SoldItems.OrderBy(t => t.Item1).ToList();

            return View(viewModel);
        }
    }
}
