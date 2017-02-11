using System;
using System.Collections.Generic;

namespace AutoAllegro.Helpers.Functions
{
    public class StatsFunctions
    {
        public static string FormatList(List<DateTime> list, string dateFormat = "MM.yyyy")
        {
            string returnList = "";

            foreach (var element in list) 
            {
                returnList += string.Format("{0}{1}{2}", "\"", element.ToString(dateFormat), "\",");
            }

            return returnList;
        }

        public static string FormatList(List<decimal> list)
        {
            string returnList = "";

            foreach (var element in list)
            {
                returnList += string.Format("{0},", element.ToString());
            }

            return returnList;
        }
    }
}