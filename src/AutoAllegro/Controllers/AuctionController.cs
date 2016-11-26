using System;
using System.Threading.Tasks;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AutoAllegro.Controllers
{
    [Authorize]
    public class AuctionController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IAllegroService _allegroService;
        private readonly UserManager<User> _userManager;

        public AuctionController(ApplicationDbContext dbContext, UserManager<User> userManager, IAllegroService allegroService)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _allegroService = allegroService;
        }

        public async Task<IActionResult> Index(int? id)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
           
            //var response = await _allegroService.Login(user.AllegroUserName, user.AllegroHashedPass, user.AllegroKey);

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
