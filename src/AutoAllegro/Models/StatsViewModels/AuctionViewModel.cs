using System.Collections.Generic;
using System;

namespace AutoAllegro.Models.StatsViewModels
{
    public class AuctionViewModel 
    {
        public List<Tuple<DateTime, int>> SoldItems { get; set; }

        public int Id { get; set; }
        public string Title { get; set; }
    }
}