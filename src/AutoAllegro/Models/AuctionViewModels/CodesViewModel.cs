using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AutoAllegro.Models.HelperModels;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class CodesViewModel : IPaginable
    {
        public IList<CodeViewModel> Codes { get; set; }
        public PaginationView PaginationSettings { get; set; }
        public string Title { get; set; }
        public int AuctionId { get; set; }
        public CodeViewMessage? Message { get; set; }
    }

    public enum CodeViewMessage
    {
        [Display(Name = "Błąd! Kod już sprzedany.", ShortName = "Error")]
        ErrorCodeSold,
        [Display(Name = "Kod usunięty pomyślnie.", ShortName = "Success")]
        SuccessDelete
    }
}