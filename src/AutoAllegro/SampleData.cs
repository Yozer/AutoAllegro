using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoAllegro
{
    internal class SampleData
    {
        const string defaultAdminUserName = "DefaultAdminUserName";
        const string defaultAdminPassword = "DefaultAdminPassword";
        const string allegroUserName = "AllegroUserName";
        const string allegroPassword = "AllegroPassword";
        const string allegroKey = "AllegroKey";

        public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider, bool createUsers = true)
        {
            using (var serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var db = serviceScope.ServiceProvider.GetService<ApplicationDbContext>();
                await db.Database.MigrateAsync();
                string id = await CreateAdminUser(serviceProvider);
                await InsertTestData(db, id);
            }
        }
        private static async Task<string> CreateAdminUser(IServiceProvider serviceProvider)
        {
            var env = serviceProvider.GetService<IHostingEnvironment>();

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            var userManager = serviceProvider.GetService<UserManager<User>>();
            builder.AddEnvironmentVariables();
            var configuration = builder.Build();

            var user = await userManager.FindByNameAsync(configuration[defaultAdminUserName]);
            if (user == null)
            {
                user = new User
                {
                    UserName = configuration[defaultAdminUserName], Email = configuration[defaultAdminPassword],
                    AllegroKey = configuration[allegroKey], AllegroUserName = configuration[allegroUserName], AllegroHashedPass = configuration[allegroPassword],
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(user, configuration[defaultAdminPassword]);
            }

            return user.Id;
        }
        private static async Task InsertTestData(ApplicationDbContext db, string userId)
        {
            if(await db.Auctions.FirstOrDefaultAsync(t => t.AllegroAuctionId == 111) != null)
                return;
            var auction1 = new Auction
            {
                UserId = userId,
                AllegroAuctionId = 111,
                CreationDate = new DateTime(2002, 12, 3, 5, 4, 2),
                EndDate = new DateTime(2011, 5, 4, 5, 6, 6),
                Fee = 50.0m,
                Title = "test ad",
                OpenCost = 51.23m,
                PricePerItem = 8.99m,
                IsVirtualItem = true,
                Converter = 1,
                GameCodes = new List<GameCode>()
            };
            for (int i = 0; i < 1000; ++i)
            {
                auction1.GameCodes.Add(new GameCode { Code = $"code - {i} - xxxx", AddDate = DateTime.Now });
            }

            var auction2 = new Auction
            {
                UserId = userId,
                AllegroAuctionId = 7731,
                CreationDate = new DateTime(1994, 12, 3, 5, 4, 2),
                EndDate = new DateTime(1998, 5, 4, 5, 6, 6),
                Fee = 513,
                Title = "test ad2",
                OpenCost = 634,
                PricePerItem = 619m,
                IsVirtualItem = false,
                Converter = 5
            };
            var auction3 = new Auction
            {
                UserId = userId,
                AllegroAuctionId = 333,
                CreationDate = new DateTime(2005, 12, 3, 5, 4, 2),
                EndDate = new DateTime(2006, 5, 4, 5, 6, 6),
                Fee = 5,
                Title = "test ad3",
                OpenCost = 5.23m,
                PricePerItem = 88.99m,
                IsVirtualItem = false,
            };
            var auction4 = new Auction
            {
                UserId = userId,
                AllegroAuctionId = 247,
                CreationDate = new DateTime(2004, 12, 3, 5, 4, 3),
                EndDate = new DateTime(2007, 5, 4, 2, 3, 6),
                Fee = 7,
                Title = "test ad4",
                OpenCost = 9.23m,
                PricePerItem = 18.99m,
                IsVirtualItem = false
            };

            db.Auctions.Add(auction1);
            db.Auctions.Add(auction2);
            db.Auctions.Add(auction3);
            db.Auctions.Add(auction4);

            // buyers
            var buyer1 = new Buyer
            {
                Email = "buyer1@gmail.com",
                Address = "Address1",
                AllegroUserId = 123,
                City = "CityBuyer1",
                FirstName = "Jacek",
                LastName = "Wojnicz",
                Phone = "141-141-2",
                PostCode = "33-114",
                UserLogin = "Pierdola",
                Phone2 = "123-123-123"
            };
            var buyer2 = new Buyer
            {
                Email = "buyer2@gmail.com",
                Address = "Address2",
                AllegroUserId = 124,
                City = "CityBuyer2",
                FirstName = "Marcin",
                LastName = "Nalepa",
                Phone = "997",
                PostCode = "33-300",
                UserLogin = "Leszczu",
                Phone2 = "142-123-123"

            };
            var buyer3 = new Buyer
            {
                Email = "buyer3@gmail.com",
                Address = "Address3",
                AllegroUserId = 125,
                City = "CityBuyer3",
                FirstName = "Tomasz",
                LastName = "Mniszek",
                Phone = "516-512-666",
                PostCode = "11-111",
                UserLogin = "Tomus",
                Phone2 = "142-123-124"
            };
            db.Buyers.Add(buyer1);
            db.Buyers.Add(buyer2);
            db.Buyers.Add(buyer3);

            db.Orders.Add(new Order
            {
                AllegroDealId = 1,
                Auction = auction1,
                Buyer = buyer1,
                OrderDate = new DateTime(1993, 12, 11, 14, 55, 22),
                OrderStatus = OrderStatus.Done,
                Quantity = 4,
                ShippingAddress = new ShippingAddress
                {
                    Address = "adres",
                    City = "city aa",
                    FirstName = "firstName",
                    LastName = "lastName",
                    MessageToSeller = "Wiadomość do Ciebie",
                    PostCode = "33-300"
                },
                GameCodes = new List<GameCode>
                {
                    auction1.GameCodes.ElementAt(0),
                    auction1.GameCodes.ElementAt(1),
                    auction1.GameCodes.ElementAt(2),
                    auction1.GameCodes.ElementAt(3),
                }
            });
            db.Orders.Add(new Order
            {
                AllegroDealId = 2,
                Auction = auction2,
                Buyer = buyer1,
                OrderDate = new DateTime(1991, 12, 11, 12, 55, 22),
                OrderStatus = OrderStatus.Paid,
                Quantity = 2,
                ShippingAddress = new ShippingAddress
                {
                    Address = "Some addr",
                    City = "Some city",
                    FirstName = "First name",
                    LastName = "Last name",
                    MessageToSeller = "Some msg",
                    PostCode = "33-300"
                }
            });
            db.Orders.Add(new Order
            {
                AllegroDealId = 3,
                Auction = auction3,
                Buyer = buyer2,
                OrderDate = new DateTime(1995, 12, 5, 12, 33, 22),
                OrderStatus = OrderStatus.Created,
                Quantity = 1
            });
            db.Orders.Add(new Order
            {
                AllegroDealId = 4,
                Auction = auction1,
                Buyer = buyer3,
                OrderDate = new DateTime(2222, 3, 5, 12, 33, 22),
                OrderStatus = OrderStatus.Created,
                Quantity = 1,
                GameCodes = new List<GameCode>
                {
                    auction1.GameCodes.ElementAt(4),
                }
            });
            db.Orders.Add(new Order
            {
                AllegroDealId = 5,
                Auction = auction4,
                Buyer = buyer3,
                OrderDate = new DateTime(2012, 3, 3, 12, 33, 11),
                OrderStatus = OrderStatus.Canceled,
                Quantity = 3
            });

            await db.SaveChangesAsync();
            //await AddOrUpdateAsync(serviceProvider, g => g.GenreId, Genres.Select(genre => genre.Value));
            //await AddOrUpdateAsync(serviceProvider, a => a.ArtistId, Artists.Select(artist => artist.Value));
            //await AddOrUpdateAsync(serviceProvider, a => a.AlbumId, albums);
        }
    }
}