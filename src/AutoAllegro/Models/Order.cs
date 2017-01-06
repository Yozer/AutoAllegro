using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoAllegro.Models
{
    public class Order
    {
        public int Id { get; set; }
        public long AllegroDealId { get; set; }
        public int? AllegroRefundId { get; set; }
        public int Quantity { get; set; }
        public DateTime OrderDate { get; set; }
        public OrderStatus OrderStatus { get; set; }

        public virtual ICollection<GameCode> GameCodes { get; set; } = new List<GameCode>();
        public virtual ICollection<Event> Events { get; set; } = new List<Event>();
        public virtual ICollection<Transaction> Transactions {get; set;} = new List<Transaction>();

        public int AuctionId { get; set; }
        public virtual Auction Auction { get; set; }
        public int BuyerId { get; set; }
        public virtual Buyer Buyer { get; set; }
        public int? ShippingAddressId { get; set; }
        public virtual ShippingAddress ShippingAddress { get; set; }

    }

    public enum OrderStatus
    {
        [Display(Name = "Rozpoczęte")]
        Created = 0,
        [Display(Name = "Zapłacone")]
        Paid = 1,
        [Display(Name = "Anulowane")]
        Canceled = 2,
        [Display(Name = "Wysłane")]
        Send = 3,
        [Display(Name = "Zakończone")]
        Done = 4
    }
}
