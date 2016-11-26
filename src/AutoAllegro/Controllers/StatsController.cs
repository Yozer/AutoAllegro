using System;
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

    }
}
