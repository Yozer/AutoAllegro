using System.Collections.Generic;
using AutoAllegro.Models.HelperModels;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class IndexViewModel : IPaginable
    {
        public IList<AuctionViewModel> Auctions { get; set; }
        public PaginationView PaginationSettings { get; set; }
    }
}
