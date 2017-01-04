using System.ComponentModel.DataAnnotations;

namespace AutoAllegro.Models.ManageViewModels
{
    public class VirtualItemSettingsViewModel
    {
        [Display(Name = "Szablon wiadomoœci")]
        public string MessageTemplate { get; set; }
        [Display(Name = "Tytu³ wiadomoœci")]
        public string MessageSubject { get; set; }
        [Display(Name = "Odpowiedz do [email]")]
        [EmailAddress(ErrorMessage = "Niepoprawny adres email.")]
        public string ReplyTo { get; set; }
        [Display(Name = "Wyœwietlana nazwa przy wys³anym mailu")]
        public string DisplayName { get; set; }
    }
}