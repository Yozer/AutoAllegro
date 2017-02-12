using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
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
            
            // Mock data
            Dictionary<DateTime, decimal> yearlyStats = new Dictionary<DateTime, decimal>();
            yearlyStats.Add(new DateTime(2016, 06, 10), 10);
            yearlyStats.Add(new DateTime(2016, 07, 10), 12);
            viewModel.YearlyStats = yearlyStats;

            var auctions = await _dbContext.Auctions.Where(t => t.UserId == _userManager.GetUserId(User)).ToListAsync();
            viewModel.Auctions = _mapper.Map<List<AuctionViewModel>>(auctions);

            return View(viewModel);
        }

        public IActionResult Daily(string date) 
        {
            var viewModel = new DailyViewModel();
            viewModel.StatsDate = date;

            // Mock data
            Dictionary<DateTime, decimal> dailyStats = new Dictionary<DateTime, decimal>();
            dailyStats.Add(new DateTime(2016, 06, 10), 10);
            dailyStats.Add(new DateTime(2016, 06, 11), 8);
            dailyStats.Add(new DateTime(2016, 06, 12), 12);
            dailyStats.Add(new DateTime(2016, 06, 13), 15);
            viewModel.DailyStats = dailyStats;

            return View(viewModel);
        }

        public IActionResult Auction(int id) 
        {
            return View();
        } 

    }
}
