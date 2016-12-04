using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AutoAllegro.Data;
using AutoAllegro.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using AutoAllegro.Models;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

using UsersContainer = System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<long, int>>;

namespace AutoAllegro.Services
{
    public class AllegroProcessor
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        // https://mgmccarthy.wordpress.com/2016/11/07/using-hangfire-to-schedule-jobs-in-asp-net-core/
        private readonly ApplicationDbContext _dbContext;
        private readonly IBackgroundJobClient _backgroundJob;
        private readonly IServiceProvider _serviceProvider;
        private readonly UsersContainer _users = new UsersContainer();

        public AllegroProcessor(ApplicationDbContext dbContext, IBackgroundJobClient backgroundJob, IServiceProvider serviceProvider)
        {
            _dbContext = dbContext;
            _backgroundJob = backgroundJob;
            _serviceProvider = serviceProvider;
        }
        public void Init()
        {
            var users = from user in _dbContext.Users.Include(t => t.Auctions)
                        from auction in user.Auctions
                        where auction.IsMonitored
                        select user;

            foreach (var user in users)
            {
                foreach (var auction in user.Auctions)
                {
                    StartProcessor(user, auction);
                }
            }
        }

        public void StartProcessor(User user, Auction auction)
        {
            var auctions = _users.GetOrAdd(user.Id, t => new ConcurrentDictionary<long, int>());
            auctions.TryAdd(auction.AllegroAuctionId, auction.Id);

            if (auctions.Count == 1)
            {
                _backgroundJob.Schedule(() => Process(user.Id), Interval);
            }
        }

        public void StopProcessor(User user, Auction auction)
        {
            var auctions = _users.GetOrAdd(user.Id, t => new ConcurrentDictionary<long, int>());
            int removed;
            auctions.TryRemove(auction.AllegroAuctionId, out removed);

            if (auctions.Count == 0)
            {
                ConcurrentDictionary<long, int> dummy;
                _users.TryRemove(user.Id, out dummy);
            }
        }

        public void Process(string userId)
        {
            var allegroService = _serviceProvider.GetService<IAllegroService>();

            if (_users.ContainsKey(userId))
                _backgroundJob.Schedule(() => Process(userId), Interval);
        }
    }
}
