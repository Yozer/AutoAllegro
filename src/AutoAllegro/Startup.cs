using System;
using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Models.AuctionViewModels;
using AutoAllegro.Models.ManageViewModels;
using AutoAllegro.Services;
using AutoAllegro.Services.AllegroProcessors;
using AutoAllegro.Services.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Hangfire.MemoryStorage;
using SoaAllegroService;

namespace AutoAllegro
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets();

                // This will push telemetry data through Application Insights pipeline faster, allowing you to view results immediately.
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
            services.AddSingleton<IUrlHelperFactory, UrlHelperFactory>();
            // Add framework services.
            services.AddApplicationInsightsTelemetry(Configuration);

            services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<User, IdentityRole>(options =>
                {
                    options.Cookies.ApplicationCookie.AccessDeniedPath = "/Home/AccessDenied";
                    options.Password.RequireDigit = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequiredLength = 5;
                    options.SignIn.RequireConfirmedEmail = true;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddMvc();
            services.AddMemoryCache();

            // Add application services.
            services.AddTransient<IEmailSender, EmailSender>();

            // add allegro service
            services.AddTransient<IAllegroService, AllegroService>();
            services.AddTransient<servicePort, servicePortClient>();

            // processors
            services.AddTransient<IAllegroFeedbackProcessor, AllegroFeedbackProcessor>();
            services.AddTransient<IAllegroTransactionProcessor, AllegroTransactionProcessor>();
            services.AddTransient<IAllegroEmailProcessor, AllegroEmailProcessor>();
            services.AddTransient<IAllegroRefundProcessor, AllegroRefundProcessor>();

            services.AddHangfire(t => t.UseMemoryStorage());
            services.AddAutoMapper(ConfigureAutoMapper);


            // Register the IConfiguration instance which MyOptions binds against.
            services.AddOptions();
            services.Configure<EmailSettings>(Configuration.GetSection("Email"));
        }

        public void ConfigureDevelopment(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            app.UseApplicationInsightsRequestTelemetry();
            loggerFactory.AddConsole(minLevel: LogLevel.Information);

            // StatusCode pages to gracefully handle status codes 400-599.
            app.UseStatusCodePagesWithRedirects("~/Home/StatusCodePage");

            // Display custom error page in production when error occurs
            // During development use the ErrorPage middleware to display error information in the browser
            app.UseDeveloperExceptionPage();

            app.UseDatabaseErrorPage();
            app.UseBrowserLink();

            Configure(app);

            //Populates the app sample data
            SampleData.InitializeDatabaseAsync(app.ApplicationServices).Wait();
        }
        public void ConfigureStaging(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(minLevel: LogLevel.Warning);

            // StatusCode pages to gracefully handle status codes 400-599.
            app.UseStatusCodePagesWithRedirects("~/Auction/StatusCodePage");

            app.UseExceptionHandler("/Home/Error");

            Configure(app);
        }
        public void ConfigureProduction(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(minLevel: LogLevel.Warning);

            // StatusCode pages to gracefully handle status codes 400-599.
            app.UseStatusCodePagesWithRedirects("~/Home/StatusCodePage");

            app.UseExceptionHandler("/Home/Error");

            Configure(app);
        }
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseHangfireDashboard();
            app.UseHangfireServer();
            app.UseApplicationInsightsExceptionTelemetry();

            //app.UseSession();
            app.UseStaticFiles();
            app.UseIdentity();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Auction}/{action=Index}/{id?}");
            });

            InitAllegroProcessor(app.ApplicationServices);
            InitAllegroRefundReasons(app.ApplicationServices);
        }

        private void InitAllegroRefundReasons(IServiceProvider appApplicationServices)
        {
            var reasons = new List<AllegroRefundReason>
            {
                new AllegroRefundReason {Id = 1, Reason = "Nie nawiązano kontaktu z Kupującym"},
                new AllegroRefundReason {Id = 2, Reason = "Nawiązano kontakt, lecz Kupujący odmówił zakupu"},
                new AllegroRefundReason {Id = 3, Reason = "Kupujący potwierdził chęć zakupu, lecz nie uiścił zapłaty"},
                new AllegroRefundReason {Id = 4, Reason = "Kupujący nie dotrzymał warunków sprzedaży zawartych w opisie przedmiotu"},
                new AllegroRefundReason {Id = 16, Reason = "Kupujący pomylił się przy składaniu oferty i zakupił więcej przedmiotów"},
                new AllegroRefundReason {Id = 11, Reason = "Przedmiot został odesłany przez Kupującego w ramach zwrotu lub reklamacji"},
            };

            List<AllegroRefundReason> existingData;
            using (var scope = appApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var db = scope.ServiceProvider.GetService<ApplicationDbContext>();
                existingData = db.AllegroRefundReasons.ToList();
            }
            using (var scope = appApplicationServices.CreateScope())
            {
                var db = scope.ServiceProvider.GetService<ApplicationDbContext>();
                foreach (var item in reasons)
                {
                    db.Entry(item).State = existingData.Any(g => g.Id.Equals(item.Id))
                        ? EntityState.Modified
                        : EntityState.Added;
                }

                db.SaveChanges();
            }
        }

        private void InitAllegroProcessor(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var allegroProcessor = scope.ServiceProvider.GetService<IAllegroTransactionProcessor>();
                allegroProcessor.Init();
                var emailProcessor = scope.ServiceProvider.GetService<IAllegroEmailProcessor>();
                emailProcessor.Init();
                var refundProcessor = scope.ServiceProvider.GetService<IAllegroRefundProcessor>();
                refundProcessor.Init();
                var feedbackProcessor = scope.ServiceProvider.GetService<IAllegroFeedbackProcessor>();
                feedbackProcessor.Init();
            }
        }

        public static void ConfigureAutoMapper(IMapperConfigurationExpression cf)
        {
            cf.CreateMap<Order, OrderViewModel>().AfterMap((order, model) =>
            {
                model.TotalPayment = order.Quantity * order.Auction.PricePerItem;
                model.VirtualItem = order.Auction.IsVirtualItem;
            }).ReverseMap();
            cf.CreateMap<Auction, AuctionViewModel>().ReverseMap();
            cf.CreateMap<VirtualItemSettings, VirtualItemSettingsViewModel>().ReverseMap();
            cf.CreateMap<CodeViewModel, GameCode>().ReverseMap();
        }
    }
}
