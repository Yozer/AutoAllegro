using System.Collections.Generic;
using AutoAllegro.Models.HelperModels;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class IndexViewModel
    {
        public IList<AuctionViewModel> Auctions { get; set; }
        public PaginationSettings PaginationSettings { get; set; }
    }
}
