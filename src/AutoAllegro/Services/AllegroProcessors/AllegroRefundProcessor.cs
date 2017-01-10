using System;
using System.Linq;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Services.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace AutoAllegro.Services.AllegroProcessors
{
    public interface IAllegroRefundProcessor : IAllegroAbstractProcessor
    {
    }

    public sealed class AllegroRefundProcessor : AllegroAbstractProcessor<IAllegroRefundProcessor>, IAllegroRefundProcessor
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        private static readonly TimeSpan MakeRefundAfter = TimeSpan.FromDays(7);
        private static readonly int RefundReasonId = 1;

        private readonly IAllegroService _allegroService;
        private readonly ILogger<AllegroRefundProcessor> _logger;
        private readonly ApplicationDbContext _db;

        public AllegroRefundProcessor(IBackgroundJobClient backgroundJob, IAllegroService allegroService, ILogger<AllegroRefundProcessor> logger, ApplicationDbContext db)
            : base(backgroundJob, logger, Interval)
        {
            _allegroService = allegroService;
            _logger = logger;
            _db = db;
        }

        protected override void Execute()
        {
            _logger.LogInformation("Starting refund processor");
            var nowDateTime = DateTime.Now;
            var refundsToMake = from order in _db.Orders
                                where order.Auction.AutomaticRefundsEnabled && order.OrderStatus == OrderStatus.Created && order.OrderDate.Add(MakeRefundAfter) <= nowDateTime
                                group order by order.Auction.UserId into g
                                select g;

            foreach (var item in refundsToMake)
            {
                string userId = item.Key;
                _logger.LogInformation($"Processing refunds for {userId}");

                var allegroCredentials = GetAllegroCredentials(_db, userId);
                _allegroService.Login(userId, allegroCredentials).Wait();

                foreach (var order in item)
                {
                    try
                    {
                        _logger.LogInformation($"Sending refund for user: {userId} and order : {order.AllegroDealId}");
                        var refundId = _allegroService.SendRefund(order, RefundReasonId).Result;
                        order.OrderStatus = OrderStatus.Canceled;
                        order.AllegroRefundId = refundId;
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(1, e, $"Error sending refund for dealId: {order.AllegroDealId}");
                    }
                }
                _db.SaveChanges();
            }
        }
    }
}
