using System;
using System.Collections.Generic;
using AutoAllegro.Models.StatsViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoAllegro.Controllers
{
    [Authorize]
    public class StatsController : Controller
    {

        public IActionResult Index()
        {
            var viewModel = new IndexViewModel();
            
            // Mock data
            Dictionary<DateTime, decimal> yearlyStats = new Dictionary<DateTime, decimal>();
            yearlyStats.Add(new DateTime(2016, 06, 10), 10);
            yearlyStats.Add(new DateTime(2016, 07, 10), 12);
            viewModel.YearlyStats = yearlyStats;

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
