using System.ComponentModel.DataAnnotations;

namespace AutoAllegro.Models.AuctionViewModels
{
    public class AddCodesViewModel
    {
        public string Title { get; set; }
        public int AuctionId { get; set; }
        [Required(ErrorMessage = "To pole jest wymagane.")]
        public string Codes { get; set; }
    }
}