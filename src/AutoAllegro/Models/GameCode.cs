namespace AutoAllegro.Models
{
    public class GameCode
    {
        public int Id { get; set; }
        public string Code { get; set; }

        public int AuctionId { get; set; }
        public virtual Auction Auction { get; set; }
        public int OrderId { get; set; }
        public virtual Order Order { get; set; }
    }
}