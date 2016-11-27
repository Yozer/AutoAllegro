using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
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

        // GET: /Auction/Add
        [HttpGet]
        public IActionResult Add()
        {

            
            List<NewAuction> list = new List<NewAuction>();

            list.Add(new NewAuction {
                Id = 1,
                Name = "Lorem Ipsum",
                Price = "19,99 zł",
                StartDate = new DateTime(2016, 12, 14),
                EndDate = new DateTime(2017, 01, 17),
                IsMonitored = true
            });

            list.Add(new NewAuction {
                Id = 2,
                Name = "Dolor Sit Amet",
                Price = "79,99 zł",
                StartDate = new DateTime(2016, 12, 10),
                EndDate = new DateTime(2017, 02, 17),
                IsMonitored = false
            });
            

            AddViewModel model = new AddViewModel{
                Auctions = list
            };

            return View(model);
        }

        // POST: /Auction/Add
        [HttpPost]
        public IActionResult Add(AddViewModel model)
        {
            return RedirectToAction(nameof(Index));
        }

    }
}
