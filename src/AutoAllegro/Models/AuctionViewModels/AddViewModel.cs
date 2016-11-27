using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;


namespace AutoAllegro.Models.AuctionViewModels
{
    public class AddViewModel
    {
        public List<NewAuction> Auctions;
    }

    public class NewAuction
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public decimal Price { get; set; }

        public bool IsMonitored { get; set; }

    }
}