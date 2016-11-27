using System;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class OrderViewModel
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public object Status { get; set; }
        public decimal TotalPayment { get; set; }
        public DateTime OrderDate { get; set; }
        public ShippingAddress ShippingAddress { get; set; }
        public Buyer Buyer { get; set; }
    }
}