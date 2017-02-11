using System.Collections.Generic;
using System;

namespace AutoAllegro.Models.StatsViewModels
{
    public class DailyViewModel 
    {
        public Dictionary<DateTime, decimal> DailyStats { get; set; }

        public string StatsDate { get; set; }
    }
}