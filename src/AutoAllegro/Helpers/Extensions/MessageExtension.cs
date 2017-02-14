using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace AutoAllegro.Helpers.Extensions
{
    public static class MessageExtensions
    {
        public static string GetStatus(this Enum enumValue)
        {
                return enumValue.GetType().GetMember(enumValue.ToString())
                   .First()
                   .GetCustomAttribute<DisplayAttribute>()
                   .ShortName;
        }

    }
}