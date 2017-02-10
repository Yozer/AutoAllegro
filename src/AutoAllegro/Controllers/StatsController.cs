using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoAllegro.Controllers
{
    [Authorize]
    public class StatsController : Controller
    {

        public IActionResult Index()
        {
            return View();
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
