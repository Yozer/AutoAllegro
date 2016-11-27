using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;


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


        [Required(ErrorMessage = "Pole hasło jest wymagane.")]
        [Display(Name = "Hasło")]           
        public string HashPassword { get; set; }
    }
}