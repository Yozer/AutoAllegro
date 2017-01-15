using System;
using System.Linq;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Services.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace AutoAllegro.Services.AllegroProcessors
{
    public interface IAllegroEmailProcessor : IAllegroAbstractProcessor
    {
    }

    public class AllegroEmailProcessor : AllegroAbstractProcessor<IAllegroEmailProcessor>, IAllegroEmailProcessor
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        private readonly IBackgroundJobClient _backgroundJob;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AllegroEmailProcessor> _logger;
        private readonly ApplicationDbContext _db;

        public AllegroEmailProcessor(IBackgroundJobClient backgroundJob, IEmailSender emailSender, ILogger<AllegroEmailProcessor> logger, ApplicationDbContext db)
            :base(backgroundJob, logger, Interval)
        {
            _backgroundJob = backgroundJob;
            _emailSender = emailSender;
            _logger = logger;
            _db = db;
        }

        public override void Init()
        {
            _backgroundJob.Schedule<IAllegroEmailProcessor>(t => t.Process(), Interval.Add(TimeSpan.FromSeconds(30)));
        }

        protected override void Execute()
        {
            var ordersToSend =  (from order in _db.Orders
                                where order.Auction.IsMonitored && order.Auction.IsVirtualItem && order.OrderStatus == OrderStatus.Paid
                                select new
                                {
                                    Order = order,
                                    order.Auction.Converter,
                                    order.Auction.User.VirtualItemSettings,
                                    order.Buyer.FirstName,
                                    order.Buyer.LastName,
                                    order.Buyer.Email
                                }).ToList();

            foreach (var item in ordersToSend)
            {
                int codesCountToSent = item.Order.Quantity * item.Converter;
                _logger.LogInformation($"Sending {codesCountToSent} codes to orderId: {item.Order.Id}");
                var codes = _db.GameCodes.Where(t => t.AuctionId == item.Order.AuctionId && t.Order == null).Take(codesCountToSent).ToList();

                if (codes.Count < codesCountToSent)
                {
                    _logger.LogWarning($"Not enough codes in database ({codes.Count}) to send to order: {item.Order.Id}");
                    continue;
                }

                string codesStr = string.Join("<br>", codes.Select(t => t.Code));
                var virtualItemSettings = item.VirtualItemSettings;
                string body = virtualItemSettings.MessageTemplate
                    .Replace("{FIRST_NAME}", item.FirstName)
                    .Replace("{LAST_NAME}", item.LastName)
                    .Replace("{QUANTITY}", item.Order.Quantity.ToString())
                    .Replace("{ITEM}", codesStr);

                try
                {
                    _emailSender.SendEmailAsync(item.Email, virtualItemSettings.MessageSubject, body, virtualItemSettings.ReplyTo, virtualItemSettings.DisplayName).Wait();
                }
                catch (Exception e)
                {
                    _logger.LogError(1, e, $"Error during sending codes for order {item.Order.Id}");
                    continue;
                }

                _logger.LogInformation($"Sent codes for orderId {item.Order.Id} successfully.");
                codes.ForEach(t => t.Order = item.Order);
                item.Order.OrderStatus = OrderStatus.Done; // skip send, because email is delivered instantly
                _db.SaveChanges();
            }
        }

    }
}
