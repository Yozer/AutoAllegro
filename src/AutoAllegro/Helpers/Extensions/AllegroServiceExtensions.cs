using System;

namespace AutoAllegro.Helpers.Extensions
{
    public static class AllegroServiceExtensions
    {
        public static DateTime ToDateTime(this long unixTimeStamp)
        {
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dateTime;
        }
    }
}