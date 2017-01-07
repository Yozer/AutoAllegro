using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AutoAllegro.Helpers.Attributes;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class OrderViewModel
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
        public OrderStatus OrderStatus { get; set; }
        [CurrencyFormat]
        public decimal TotalPayment { get; set; }
        public DateTime OrderDate { get; set; }
        public ShippingAddress ShippingAddress { get; set; }
        public Buyer Buyer { get; set; }
        public bool VirtualItem { get; set; }
        public int AuctionId { get; set; }
        public List<CodeViewModel> GameCodes { get; set; } = new List<CodeViewModel>();
        public List<AllegroRefundReason> RefundReasons { get; set; }
        public OrderViewMessage? Message { get; set; }
    }

    public enum OrderViewMessage
    {
        [Display(Name = "Zamówienie anulowano pomyślnie.")]
        OrderCancelSuccess,
        [Display(Name = "Kod wygnerowany pomyślnie.")]
        GenerateCodeSuccess,
        [Display(Name = "Błąd. Dla tej aukcji nie ma już dostępnych kodów. Dodaj nowe.")]
        GenerateCodeNoCodesAvailable,
        [Display(Name = "Błąd. Nie możesz anulować już anulowanego zamówienia.")]
        OrderCancelFail,
        [Display(Name = "Błąd podczas wysyłania zwrotu prowizji do Allegro.")]
        SendingRefundFailed,
        [Display(Name = "Zamówienie oznaczone jako opłacone.")]
        OrderMarkedAsPaid,
        [Display(Name = "Błąd podczas anulowania zwrotu prowizji. Prawdopodobnie kupujący otrzymał już ostrzeżenie od Allegro.")]
        CannotMarkAsPaid
    }
}