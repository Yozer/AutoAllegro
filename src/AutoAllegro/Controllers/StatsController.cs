using System;
using Microsoft.AspNetCore.Mvc;

namespace AutoAllegro.Controllers {
    public class StatsController : Controller {

        public IActionResult index() {
            return View();
        }

    }
}
