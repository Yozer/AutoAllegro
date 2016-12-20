using System;
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
using AutoAllegro.Services;
using AutoAllegro.Services.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Hangfire.MemoryStorage;

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
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddMvc();
            services.AddMemoryCache();

            // Add application services.
            services.AddTransient<IEmailSender, AuthMessageSender>();

            // add allegro service
            services.AddTransient<IAllegroService, AllegroService>();
            services.AddAutoMapper(cf =>
            {
                cf.CreateMap<Order, OrderViewModel>().AfterMap((order, model) =>
                {
                    model.TotalPayment = order.Quantity*order.Auction.PricePerItem;
                    model.VirtualItem = order.Auction.IsVirtualItem;
                }).ReverseMap();
                cf.CreateMap<Auction, AuctionViewModel>().ReverseMap();
            });

            services.AddSingleton<AllegroProcessor>();
            services.AddHangfire(t => t.UseMemoryStorage());
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

            GlobalConfiguration.Configuration.UseActivator(new ContainerJobActivator(app.ApplicationServices));
            InitAllegroProcessor(app.ApplicationServices);
        }

        private void InitAllegroProcessor(IServiceProvider serviceProvider)
        {
            var allegroProcessor = serviceProvider.GetService<AllegroProcessor>();
            allegroProcessor.Init();
        }
    }

    public class ContainerJobActivator : JobActivator
    {
        private readonly IServiceProvider _serviceProvider;

        public ContainerJobActivator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public override object ActivateJob(Type type)
        {
            return _serviceProvider.GetService(type);
        }
    }
}
