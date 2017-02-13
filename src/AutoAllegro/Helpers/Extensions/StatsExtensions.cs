using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoAllegro.Helpers.Extensions
{
    public static class StatsExtensions
    {
        public static string FormatDates<T>(this List<Tuple<DateTime, T>> data, string format = "MMMM yyyy")
        {
            return string.Join(",", data.Select(t => $"\"{t.Item1.ToString(format)}\""));
        }

        public static string FormatDecimals(this List<Tuple<DateTime, decimal>> data, string format = "0.00")
        {
            return string.Join(",", data.Select(t => t.Item2.ToString(format, new CultureInfo("en-US"))));
        }
        public static string FormatInts(this List<Tuple<DateTime, int>> data)
        {
            return string.Join(",", data.Select(t => t.Item2.ToString()));
        }
    }
}