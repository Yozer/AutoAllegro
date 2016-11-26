using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoAllegro.Controllers
{
    [Authorize]
    public class AuctionController : Controller
    {

        public IActionResult Index(int? id)
        {
            return View();
        }

        public IActionResult Auction(int? id)
        {
            return View();
        }

        public IActionResult Order(int? id)
        {
            return View();
        }

        public IActionResult Add()
        {
            return View();
        }

    }
}
