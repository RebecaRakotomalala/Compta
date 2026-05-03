using System.ComponentModel.DataAnnotations;

namespace dadaApp.Models;

public class RegisterViewModel
{
    [Required(ErrorMessage = "L’identifiant est obligatoire.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Entre 2 et 80 caractères.")]
    [Display(Name = "Identifiant")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(200, MinimumLength = 4, ErrorMessage = "Au moins 4 caractères.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirmez le mot de passe.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmation")]
    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
