using System;

namespace AutoAllegro.Models
{
    public class Event
    {
        public int Id { get; set; }
        public long AllegroEventId { get; set; }
        public EventType EventType { get; set; }
        public DateTime EventTime { get; set; }

        public int OrderId { get; set; }
        public virtual Order Order { get; set; }
    }

    public enum EventType
    {
        /// <summary>
        /// Utworzenie aktu zakupowego (deala)
        /// </summary>
        DealCreated = 1,
        /// <summary>
        /// Utworzenie formularza pozakupowego (transakcji)
        /// </summary>
        TransactionCreated = 2,
        /// <summary>
        /// Anulowanie formularza pozakupowego (transakcji)
        /// </summary>
        TransactionCanceled = 3,
        /// <summary>
        /// Zakończenie (opłacenie) transakcji przez PzA)
        /// </summary>
        TransactionFinished = 4
    }
}
