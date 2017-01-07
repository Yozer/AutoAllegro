namespace AutoAllegro.Models
{
    public class AuctionBuyerFeedback
    {
        public int Id { get; set; }
        public int AllegroFeedbackId { get; set; }
        public int BuyerId { get; set; }
        public virtual Buyer Buyer { get; set; }
        public int AuctionId { get; set; }
        public virtual Auction Auction { get; set; }
    }
}