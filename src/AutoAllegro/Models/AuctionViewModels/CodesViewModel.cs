using System;
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

    public class CodeViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public DateTime AddDate { get; set; }
        public int? OrderId { get; set; }
        public int AuctionId { get; set; }
    }

    public enum CodeViewMessage
    {
        [Display(Name = "Błąd! Prawdopodobnie nie masz dostępu")]
        ErrorNoAccess,
        [Display(Name = "Kod usunięty pomyślnie")]
        SuccessDelete
    }
}