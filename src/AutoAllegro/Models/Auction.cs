using System;
using System.Collections.Generic;
using System.IO;

namespace AutoAllegro.Models
{
    public class Auction
    {
        public int Id { get; set; }
        public PathTooLongException AllegroAuctionId { get; set; }
        public string Title { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal CostPerItem { get; set; }
        public bool IsMonitored { get; set; }
        public int Converter { get; set; }
        public decimal Fee { get; set; }
        public decimal OpenCost { get; set; }

        public int UserId { get; set; }
        public virtual User User { get; set; }

        public virtual ICollection<Order> Orders { get; set; }
        public virtual ICollection<GameCode> GameCodes { get; set; }
    }
}
