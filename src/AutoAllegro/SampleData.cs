using System;
using System.Collections.Generic;
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
                await InsertTestData(serviceProvider);
                if (createUsers)
                {
                    await CreateAdminUser(serviceProvider);
                }
            }
        }
        private static async Task CreateAdminUser(IServiceProvider serviceProvider)
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
        }
        private static Task InsertTestData(IServiceProvider serviceProvider)
        {
            return Task.CompletedTask;
            //await AddOrUpdateAsync(serviceProvider, g => g.GenreId, Genres.Select(genre => genre.Value));
            //await AddOrUpdateAsync(serviceProvider, a => a.ArtistId, Artists.Select(artist => artist.Value));
            //await AddOrUpdateAsync(serviceProvider, a => a.AlbumId, albums);
        }
    }
}