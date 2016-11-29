using AutoAllegro.Models.HelperModels;

namespace AutoAllegro.Models.AuctionViewModels
{
    public interface IPaginable
    {
        PaginationView PaginationSettings { get; set; }
    }
}