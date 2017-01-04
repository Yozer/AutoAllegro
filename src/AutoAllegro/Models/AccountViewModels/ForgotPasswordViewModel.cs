using System;
using System.ComponentModel.DataAnnotations;

namespace AutoAllegro.Models.AccountViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Pole e-mail jest wymagane.")]
        [EmailAddress(ErrorMessage = "Niepoprawny e-mail")]
        public string Email { get; set; }
    }
}
