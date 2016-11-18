using System;
using Microsoft.AspNetCore.Mvc;

namespace AutoAllegro.Controllers {
    public class AuctionController : Controller {

        public IActionResult index(int? id) {
            return View();
        }

        public IActionResult auction(int? id) {
            return View();
        }

        public IActionResult order(int? id) {
            return View();
        }

        public IActionResult add() {
            return View();
        }

    }
}
