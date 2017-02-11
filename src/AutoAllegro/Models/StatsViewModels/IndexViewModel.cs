using System.Collections.Generic;
using System;

namespace AutoAllegro.Models.StatsViewModels
{
    public class IndexViewModel 
    {
        public Dictionary<DateTime, decimal> YearlyStats { get; set; }
    }
}