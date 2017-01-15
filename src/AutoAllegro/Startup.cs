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
using Hangfire.Common;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Hangfire.MemoryStorage;
using Hangfire.States;
using Hangfire.Storage;
using NuGet.Packaging;
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
                .AddEnvironmentVariables()
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
            HostingEnvironment = env;
        }

        public IConfigurationRoot Configuration { get; }
        public IHostingEnvironment HostingEnvironment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
            services.AddSingleton<IUrlHelperFactory, UrlHelperFactory>();
            // Add framework services.
            services.AddApplicationInsightsTelemetry(Configuration);

            if (HostingEnvironment.IsDevelopment())
            {
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));
            }
            else
            {
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
            }

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

            if (HostingEnvironment.IsDevelopment())
            {
                services.AddHangfire(t => t.UseMemoryStorage());
            }
            else
            {
                services.AddHangfire(t => t.UseSqlServerStorage(Configuration.GetConnectionString("DefaultConnection")));
            }
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
        }
        public void ConfigureStaging(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            app.UseApplicationInsightsRequestTelemetry();
            loggerFactory.AddConsole(minLevel: LogLevel.Warning);

            // StatusCode pages to gracefully handle status codes 400-599.
            app.UseStatusCodePagesWithRedirects("~/Auction/StatusCodePage");

            app.UseExceptionHandler("/Home/Error");

            Configure(app);
        }
        public void ConfigureProduction(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            app.UseApplicationInsightsRequestTelemetry();
            loggerFactory.AddConsole(minLevel: LogLevel.Warning);
            loggerFactory.AddAzureWebAppDiagnostics();

            // StatusCode pages to gracefully handle status codes 400-599.
            app.UseStatusCodePagesWithRedirects("~/Home/StatusCodePage");

            app.UseExceptionHandler("/Home/Error");

            Configure(app);
        }
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseApplicationInsightsExceptionTelemetry();
            app.UseHangfireServer();

            app.UseStaticFiles();
            app.UseIdentity();

            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new MyRestrictiveAuthorizationFilter() }
            });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Auction}/{action=Index}/{id?}");
            });

            //Populates the app sample data
            SampleData.InitializeDatabaseAsync(app.ApplicationServices, HostingEnvironment).Wait();
            InitHangfire(app.ApplicationServices);
        }

        private static readonly List<Type> Jobs = new List<Type>
        {
            typeof(IAllegroTransactionProcessor),
            typeof(IAllegroEmailProcessor),
            typeof(IAllegroRefundProcessor),
            typeof(IAllegroFeedbackProcessor)
        };
        private void InitHangfire(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                HashSet<Type> scheduledJobs = new HashSet<Type>();

                IMonitoringApi api = JobStorage.Current.GetMonitoringApi();
                scheduledJobs.AddRange(api.ScheduledJobs(0, 1000).Select(t => t.Value.Job.Type));
                scheduledJobs.AddRange(api.ProcessingJobs(0, 1000).Select(t => t.Value.Job.Type));
                scheduledJobs.AddRange(api.EnqueuedJobs("default", 0, 1000).Select(t => t.Value.Job.Type));

                foreach (var jobType in Jobs.Where(t => !scheduledJobs.Contains(t)))
                {
                    var processor = (IAllegroAbstractProcessor)scope.ServiceProvider.GetService(jobType);
                    processor.Init();
                }
            }

            GlobalJobFilters.Filters.Add(new ProlongExpirationTimeAttribute());
        }

        public static void ConfigureAutoMapper(IMapperConfigurationExpression cf)
        {
            cf.CreateMap<Order, OrderViewModel>().AfterMap((order, model) =>
            {
                model.TotalPayment = order.Quantity * order.Auction.PricePerItem;
                model.VirtualItem = order.Auction.IsVirtualItem;
            }).ReverseMap();
            cf.CreateMap<Auction, AuctionViewModel>().AfterMap((auction, model) =>
            {
                model.Profit = auction.Orders.Where(t => t.OrderStatus == OrderStatus.Done).Sum(t => t.Quantity) * auction.PricePerItem - auction.Fee - auction.OpenCost;
            }).ReverseMap();
            cf.CreateMap<VirtualItemSettings, VirtualItemSettingsViewModel>().AfterMap((settings, model) =>
            {
                model.MessageTemplate = settings.MessageTemplate.Replace("<br>", "\r\n");
            }).ReverseMap().AfterMap((model, settings) =>
            {
                settings.MessageTemplate = model.MessageTemplate.Replace("\r\n", "<br>");
            });

            cf.CreateMap<CodeViewModel, GameCode>().ReverseMap();
        }

        public class MyRestrictiveAuthorizationFilter : IDashboardAuthorizationFilter
        {
            public bool Authorize(DashboardContext context)
            {
                var httpContext = context.GetHttpContext();
                return httpContext.User.Identity.IsAuthenticated;
            }
        }
        public class ProlongExpirationTimeAttribute : JobFilterAttribute, IApplyStateFilter
        {
            public void OnStateApplied(ApplyStateContext filterContext, IWriteOnlyTransaction transaction)
            {
                filterContext.JobExpirationTimeout = TimeSpan.FromHours(2);
            }

            public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
            {
                context.JobExpirationTimeout = TimeSpan.FromHours(2);
            }
        }
    }
}
