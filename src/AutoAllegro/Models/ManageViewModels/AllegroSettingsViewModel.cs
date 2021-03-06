using System;
using System.ComponentModel.DataAnnotations;


namespace AutoAllegro.Models.ManageViewModels
{
    public class AllegroSettingsViewModel
    {
        [Required(ErrorMessage = "Pole klucz API jest wymagane.")]
        [Display(Name = "Klucz API")]
        public string ApiKey { get; set; }
        
        [Required(ErrorMessage = "Pole login jest wymagane.")]
        [Display(Name = "Login")]        
        public string Login { get; set; }


        [Required(ErrorMessage = "Pole hash hasła jest wymagane.")]
        [Display(Name = "Hash hasła")]           
        public string HashPassword { get; set; }
    }
}