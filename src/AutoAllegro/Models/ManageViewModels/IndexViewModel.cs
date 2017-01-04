using System;
using System.ComponentModel.DataAnnotations;
using AutoAllegro.Controllers;

namespace AutoAllegro.Models.ManageViewModels
{
    public class IndexViewModel
    {
        public ManageMessageId? Message { get; set; }
    }

    public enum ManageMessageId
    {
        [Display(Name = "Twoje hasło zostało zmienione.")]
        ChangePasswordSuccess,
        [Display(Name = "Wystąpił błąd.")]
        Error,
        [Display(Name = "Dane dostępowe do Allegro zostały zmienione pomyślnie.")]
        ChangedAllegroSettings,
        [Display(Name = "Ustawienia wirtualnych przedmiotów zostały zmienione pomyślnie.")]
        ChangedVirtualItemSettings
    }
}
