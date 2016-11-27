using System;
using System.Collections.Generic;
using AutoAllegro.Models.HelperModels;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class AuctionViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public long AllegroId { get; set; }
        public decimal PricePerItem { get; set; }
        public decimal Fee { get; set; }
        public decimal OpenCost { get; set; }
        public DateTime EndDate { get; set; }
        public IList<OrderViewModel> Orders { get; set; }
        public PaginationSettings PaginationSettings { get; set; }
    }
}
