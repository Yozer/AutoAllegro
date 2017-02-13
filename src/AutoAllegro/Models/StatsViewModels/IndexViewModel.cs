using System.Collections.Generic;
using System;

namespace AutoAllegro.Models.StatsViewModels
{
    public class IndexViewModel 
    {
        public List<Tuple<DateTime, decimal>> YearlyStats { get; set; }

        public List<Models.AuctionViewModels.AuctionViewModel> Auctions { get; set; }
    }
}