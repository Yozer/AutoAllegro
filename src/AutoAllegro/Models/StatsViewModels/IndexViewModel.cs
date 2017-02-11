using System.Collections.Generic;
using System;
using AutoAllegro.Models.AuctionViewModels;

namespace AutoAllegro.Models.StatsViewModels
{
    public class IndexViewModel 
    {
        public Dictionary<DateTime, decimal> YearlyStats { get; set; }

        public List<AuctionViewModel> Auctions { get; set; }
    }
}