using System;
using System.Linq;
using System.ServiceModel;
using AutoAllegro.Data;
using AutoAllegro.Models;
using AutoAllegro.Services.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace AutoAllegro.Services
{
    public class AllegroRefundProcessor : IAllegroRefundProcessor
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        private static readonly TimeSpan MakeRefundAfter = TimeSpan.FromDays(7);
        private static readonly int RefundReasonId = 1;

        private readonly IBackgroundJobClient _backgroundJob;
        private readonly IAllegroService _allegroService;
        private readonly ILogger<AllegroRefundProcessor> _logger;
        private readonly ApplicationDbContext _db;

        public AllegroRefundProcessor(IBackgroundJobClient backgroundJob, IAllegroService allegroService, ILogger<AllegroRefundProcessor> logger, ApplicationDbContext db)
        {
            _backgroundJob = backgroundJob;
            _allegroService = allegroService;
            _logger = logger;
            _db = db;
        }

        public void Init()
        {
            _backgroundJob.Schedule(() => Process(), Interval);
        }
        public void Process()
        {
            try
            {
                MakeRefunds();
            }
            catch (TimeoutException e)
            {
                _logger.LogError(1, e, "The service operation timed out.");
            }
            catch (FaultException e)
            {
                _logger.LogError(1, e, "An unknown exception was received.");
            }
            catch (CommunicationException e)
            {
                _logger.LogError(1, e, "There was a communication problem.");
            }

            _backgroundJob.Schedule(() => Process(), Interval);
        }

        private void MakeRefunds()
        {
            _logger.LogInformation("Starting refund processor");
            var nowDateTime = DateTime.Now;
            var refundsToMake = from order in _db.Orders
                                where order.Auction.IsMonitored && order.OrderStatus == OrderStatus.Created && order.OrderDate.Add(MakeRefundAfter) <= nowDateTime
                                group order by order.Auction.UserId into g
                                select g;

            foreach (var item in refundsToMake)
            {
                string userId = item.Key;
                _logger.LogInformation($"Processing refunds for {userId}");

                if (_allegroService.IsLoginRequired(userId))
                {
                    var allegroCredentials = GetAllegroCredentials(_db, userId);
                    _allegroService.Login(userId, allegroCredentials).Wait();
                }

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
        private AllegroCredentials GetAllegroCredentials(ApplicationDbContext db, string id)
        {
            var user = db.Users.Single(t => t.Id == id);
            return new AllegroCredentials(user.AllegroUserName, user.AllegroHashedPass, user.AllegroKey, user.AllegroJournalStart);
        }
    }
}
