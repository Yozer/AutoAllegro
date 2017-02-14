using System;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class GetAuctionViewModel
    {
        public int Id { get; set; }
        public int? Page { get; set; }
        public bool RefreshFees { get; set; }
        public bool RefreshAd { get; set; }
        public AuctionMessageId? Message { get; set; }
        public bool SettingsTabActive { get; set; }
        public string SearchString { get; set; }
    }
}
