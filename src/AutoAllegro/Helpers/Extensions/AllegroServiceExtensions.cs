﻿using System;

namespace AutoAllegro.Helpers.Extensions
{
    public static class AllegroServiceExtensions
    {
        private static readonly DateTime UnixDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
        public static DateTime ToDateTime(this long unixTimeStamp)
        {
            return UnixDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
        }
    }
}