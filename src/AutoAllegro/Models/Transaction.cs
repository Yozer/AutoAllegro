namespace AutoAllegro.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public long AllegroTransactionId { get; set; }
        public TransactionStatus TransactionStatus { get; set; }

        public int OrderId { get; set; }
        public virtual Order Order { get; set; }
    }

    public enum TransactionStatus
    {
        Created = 1,
        Canceled = 2,
        Finished = 3
    }
}