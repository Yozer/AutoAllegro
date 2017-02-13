using System.Collections.Generic;
using System;

namespace AutoAllegro.Models.StatsViewModels
{
    public class AuctionViewModel 
    {
        public Dictionary<DateTime, decimal> SaledItems { get; set; }

        public Auction Auction { get; set; }
    }
}