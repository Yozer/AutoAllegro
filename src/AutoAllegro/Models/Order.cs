using System;
using System.Collections.Generic;

namespace AutoAllegro.Models
{
    public class Order
    {
        public int Id { get; set; }
        public long AllegroDealId { get; set; }
        public int Quantity { get; set; }
        public DateTime OrderDate { get; set; }
        public OrderStatus OrderStatus { get; set; }

        public virtual ICollection<GameCode> GameCodes { get; set; }
        public virtual ICollection<Event> Events { get; set; }
        public virtual ICollection<Transaction> Transactions {get; set;}

        public int AuctionId { get; set; }
        public virtual Auction Auction { get; set; }
        public int BuyerId { get; set; }
        public virtual Buyer Buyer { get; set; }
        public int ShippingAddressId { get; set; }
        public virtual ShippingAddress ShippingAddress { get; set; }

    }

    public enum OrderStatus
    {
        Created = 0,
        Paid = 1,
        Canceled = 2,
        Send = 3,
        Done = 4
    }
}
