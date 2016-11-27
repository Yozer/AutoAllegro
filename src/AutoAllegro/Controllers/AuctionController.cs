using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Models.HelperModels;
using AutoAllegro.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        public async Task<IActionResult> Index(int? page)
        {
            const int pageSize = 15;
            var auctions = await GetUserAuctions();
            if (auctions == null)
            {
                return RedirectToAction(nameof(AccountController.Login), "Account");
            }

            page = page ?? 1;
            --page;
            int from = Math.Max(0, page.Value * pageSize);
            var view = new IndexViewModel
            {
                PaginationSettings = new PaginationSettings
                {
                    CurrentPage = page.Value + 1,
                    PagesCount = Math.Max(1, (int) Math.Ceiling(auctions.Count/(decimal) pageSize))
                },
                Auctions = auctions.Skip(from).Take(pageSize).Select(t => new AuctionViewModel
                {
                    Id = t.Id,
                    Title = t.Title,
                    AllegroId = t.AllegroAuctionId,
                    PricePerItem = t.CostPerItem
                }).ToList()
            };

            return View(view);
        }


        public async Task<IActionResult> Auction(int id, int? page)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Index));
            }

            const int pageSize = 25;
            var auction = await GetUserAuction(id);
            auction = new Auction
            {
                AllegroAuctionId = 5125,
                Id = 5,
                Title = "adsasdasd",
                CostPerItem = 5.15m,
                EndDate = DateTime.Now,
                Fee = 512.3m,
                IsMonitored = true,
                OpenCost = 7m,
                Orders = new List<Order>
                {
                    new Order {OrderDate = DateTime.Now, Quantity = 5, Buyer = new Buyer {Email = "qeqweeqwe@wp.pl", UserLogin = "eqweqwqew"} },
                }
            };
            if (auction == null)
            {
                return RedirectToAction(nameof(Index));
            }

            for (int i = 0; i < 500; ++i)
                auction.Orders.Add(new Order { OrderDate = DateTime.Now, Quantity = 5, Buyer = new Buyer { Email = "qeqweeqwe@wp.pl", UserLogin = "eqweqwqew" } });

            page = page ?? 1;
            --page;
            int from = Math.Max(0, page.Value * pageSize);

            return View(new AuctionViewModel
            {
                Id = auction.Id,
                Title = auction.Title,
                AllegroId = auction.AllegroAuctionId,
                PricePerItem = auction.CostPerItem,
                Fee = auction.Fee,
                OpenCost = auction.OpenCost,
                EndDate = auction.EndDate,
                PaginationSettings = new PaginationSettings
                {
                    CurrentPage = page.Value + 1,
                    PagesCount = Math.Max(1, (int)Math.Ceiling(auction.Orders.Count / (decimal)pageSize))
                },
                Orders = auction.Orders.Skip(from).Take(pageSize).Select(t => new OrderViewModel
                {
                    Id = t.Id,
                    Buyer = t.Buyer,
                    OrderDate = t.OrderDate,
                    Quantity = t.Quantity,
                    TotalPayment = t.Quantity * auction.CostPerItem,
                    Status = GetOrderStatus(t)
                }).ToList(),
            });
        }

        public async Task<IActionResult> Order(int id)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Index));
            }

            var order = await GetUserOrder(id);
            order = new Order
            {
                Auction = new Auction {CostPerItem = 25.1m},
                Buyer = new Buyer
                {
                    Email = "eqwew@wp.pl",
                    UserLogin = "eqweqw",
                    Phone = "515-560-352",
                    Address = "Gówno 2/3",
                    City = "Nowy S¹cz",
                    PostCode = "00-000",
                    FirstName = "Tomasz",
                    LastName = "Mniszek"
                },
                OrderDate = DateTime.Now,
                Quantity = 5,
                ShippingAddress = new ShippingAddress { Address = "addresdasdasdd", City = "Kraków", PostCode = "33-300", FirstName = "Dominik", LastName = "Baran"}
            };
            if (order == null)
            {
                return RedirectToAction(nameof(Index));
            }

            return View(new OrderViewModel
            {
                Id = order.Id,
                OrderDate = order.OrderDate,
                Quantity = order.Quantity,
                TotalPayment = order.Quantity*order.Auction.CostPerItem,
                ShippingAddress = order.ShippingAddress,
                Buyer = order.Buyer,
                Status = GetOrderStatus(order)
            });
        }
        public IActionResult Add()
        {

            
            List<NewAuction> list = new List<NewAuction>();

            list.Add(new NewAuction {
                Id = 1,
                Name = "Lorem Ipsum",
                Price = "19,99 z³",
                StartDate = new DateTime(2016, 12, 14),
                EndDate = new DateTime(2017, 01, 17),
                IsMonitored = true
            });

            list.Add(new NewAuction {
                Id = 2,
                Name = "Dolor Sit Amet",
                Price = "79,99 z³",
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

        private string GetOrderStatus(Order order)
        {
            if (order.OrderStatus == OrderStatus.Created)
                return "Rozpoczête";
            if (order.OrderStatus == OrderStatus.Paid)
                return "Transakcja rozpoczêta";
            else if (order.OrderStatus == OrderStatus.Canceled)
                return "Transakcja anulowana";
            else if (order.OrderStatus == OrderStatus.Send)
                return "Zamówienie wys³ane";
            else if (order.OrderStatus == OrderStatus.Done)
                return "Zakoñczone";

            throw new ArgumentException(nameof(order));
        }

        private Task<string> GetUserId()
        {
            return _userManager.GetUserAsync(HttpContext.User).ContinueWith(t => t.Result?.Id);
        }
        private Task<ICollection<Auction>> GetUserAuctions()
        {
            return GetUserId()
                .ContinueWith(task => _dbContext.Users.Include(t => t.Auctions).Where(t => t.Id == task.Result).SingleOrDefaultAsync()
                .ContinueWith(t => t.Result?.Auctions))
                .Unwrap();
        }
        private Task<Auction> GetUserAuction(int id)
        {
            return GetUserId()
                .ContinueWith(task => _dbContext.Users.Include(t => t.Auctions)
                .Where(t => t.Id == task.Result).SelectMany(t => t.Auctions)
                .Where(t => t.Id == id).Include(t => t.Orders)
                .SingleOrDefaultAsync())
                .Unwrap();
        }

        private Task<Order> GetUserOrder(int id)
        {
            return GetUserId()
                .ContinueWith(task => _dbContext.Users.Include(t => t.Auctions)
                    .Where(t => t.Id == task.Result).SelectMany(t => t.Auctions)
                    .Include(t => t.Orders).SelectMany(t => t.Orders)
                    .Include(t => t.ShippingAddress).Where(t => t.Id == id)
                    .SingleOrDefaultAsync())
                .Unwrap();
        }
    }
}
