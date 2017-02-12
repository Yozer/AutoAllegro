using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoAllegro.Helpers.Extensions
{
    public static class StatsExtensions
    {
        public static string FormatDates(this Dictionary<DateTime, decimal> data, string format = "dd.MM")
        {
            return string.Join(",", data.Keys.Select(t => $"\"{t.Date.ToString(format)}\""));
        }

        public static string FormatDecimals(this Dictionary<DateTime, decimal> data, string format = "{0:0.00} z³")
        {
            return string.Join(",", data.Values.Select(t => t.ToString(format)));
        }
    }
}