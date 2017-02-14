using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AutoAllegro.Helpers.Attributes;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class OrderViewModel
    {
        public int Id { get; set; }
        public long AllegroDealId { get; set; }
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
        public OrderViewMessage? Message { get; set; }
    }

    public enum OrderViewMessage
    {
        [Display(Name = "Zamówienie anulowano pomyślnie.", ShortName = "Success")]
        OrderCancelSuccess,
        [Display(Name = "Kod wygnerowany pomyślnie.", ShortName = "Success")]
        GenerateCodeSuccess,
        [Display(Name = "Błąd. Dla tej aukcji nie ma już dostępnych kodów. Dodaj nowe.", ShortName = "Error")]
        GenerateCodeNoCodesAvailable,
        [Display(Name = "Błąd. Nie możesz anulować już anulowanego zamówienia.", ShortName = "Error")]
        OrderCancelFail,
        [Display(Name = "Błąd podczas wysyłania zwrotu prowizji do Allegro.", ShortName = "Error")]
        SendingRefundFailed,
        [Display(Name = "Zamówienie oznaczone jako opłacone.", ShortName = "Success")]
        OrderMarkedAsPaid,
        [Display(Name = "Błąd podczas anulowania zwrotu prowizji. Prawdopodobnie kupujący otrzymał już ostrzeżenie od Allegro.", ShortName = "Error")]
        CannotMarkAsPaid,
        [Display(Name = "Pomyślnie zwolniono kody.", ShortName = "Success")]
        FreeCodesSuccess,
        [Display(Name = "Błąd. Kody można zwolnić tylko dla anulowanego zamówienia.", ShortName = "Error")]
        FreeCodesOnlyForCanceledOrder
    }
}