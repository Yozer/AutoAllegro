using System;
using System.Collections.Generic;
using AutoAllegro.Models.HelperModels;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class AuctionViewModel : IPaginable
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public long AllegroAuctionId { get; set; }
        public decimal PricePerItem { get; set; }
        public decimal Fee { get; set; }
        public decimal OpenCost { get; set; }
        public DateTime EndDate { get; set; }
        public IList<OrderViewModel> Orders { get; set; }
        public PaginationView PaginationSettings { get; set; }
    }
}
