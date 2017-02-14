using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AutoAllegro.Helpers.Attributes;
using AutoAllegro.Models.HelperModels;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class AuctionViewModel : IPaginable
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public long AllegroAuctionId { get; set; }
        [CurrencyFormat]
        public decimal PricePerItem { get; set; }
        [CurrencyFormat]
        public decimal Fee { get; set; }
        [CurrencyFormat]
        public decimal OpenCost { get; set; }
        [CurrencyFormat]
        public decimal Profit { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreationDate { get; set; }
        public IList<OrderViewModel> Orders { get; set; }
        public PaginationView PaginationSettings { get; set; }
        public bool IsMonitored { get; set; }
        public bool IsVirtualItem { get; set; }
        public bool AutomaticFeedbackEnabled { get; set; }
        public bool AutomaticRefundsEnabled { get; set; }
        public bool HasEnded { get; set; }
        public AuctionMessageId? Message { get; set; }
        public bool SettingsTabActive { get; set; }
        public int FreeCodesCount { get; set; }
        public string SearchString { get; set;}
    }

    public enum AuctionMessageId
    {
        [Display(Name = "Zanim włączysz opcję \"Wirtualny przedmiot\" ustaw dane do wysyłki maili w ustawieniach konta.", ShortName = "Error")]
        CannotSetVirtualItem,
        [Display(Name = "Ustawienia zapisane pomyślnie.", ShortName = "Success")]
        SuccessSaveSettings,
        [Display(Name = "Kody zostały dodane pomyślnie.", ShortName = "Success")]
        SuccessAddCodes
    }
}
