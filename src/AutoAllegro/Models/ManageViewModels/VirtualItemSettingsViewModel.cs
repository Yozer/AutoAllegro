using System.ComponentModel.DataAnnotations;

namespace AutoAllegro.Models.ManageViewModels
{
    public class VirtualItemSettingsViewModel
    {
        [Display(Name = "Szablon wiadomo�ci")]
        public string MessageTemplate { get; set; }
        [Display(Name = "Tytu� wiadomo�ci")]
        public string MessageSubject { get; set; }
        [Display(Name = "Odpowiedz do [email]")]
        [EmailAddress(ErrorMessage = "Niepoprawny adres email.")]
        public string ReplyTo { get; set; }
        [Display(Name = "Wy�wietlana nazwa przy wys�anym mailu")]
        public string DisplayName { get; set; }
    }
}