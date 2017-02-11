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
            return View();
        }

        public IActionResult Auction(int id) 
        {
            return View();
        } 

    }
}
