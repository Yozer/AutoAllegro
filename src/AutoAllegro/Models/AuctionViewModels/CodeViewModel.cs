using System;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class CodeViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public DateTime AddDate { get; set; }
        public int AuctionId { get; set; }
    }
}