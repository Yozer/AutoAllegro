using System.ComponentModel.DataAnnotations;

namespace AutoAllegro.Helpers.Attributes
{
    public class CurrencyFormatAttribute : DisplayFormatAttribute
    {
        public CurrencyFormatAttribute()
        {
            DataFormatString = "{0:0.00} z³";
        }
    }
}