using System;
using Microsoft.AspNetCore.Mvc;

namespace AutoAllegro.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Error()
        {
            return View();
        }

        public IActionResult StatusCodePage()
        {
            return View();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
