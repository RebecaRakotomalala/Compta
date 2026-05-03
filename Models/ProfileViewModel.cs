using System.ComponentModel.DataAnnotations;

namespace dadaApp.Models;

public class ProfileViewModel : IValidatableObject
{
    [Required(ErrorMessage = "L’identifiant est obligatoire.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Entre 2 et 80 caractères.")]
    [Display(Name = "Identifiant")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Saisissez votre mot de passe actuel pour valider les changements.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mot de passe actuel")]
    public string CurrentPassword { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Trop long.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nouveau mot de passe (optionnel)")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirmer le nouveau mot de passe")]
    public string? ConfirmNewPassword { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            if (!string.IsNullOrWhiteSpace(ConfirmNewPassword))
                yield return new ValidationResult(
                    "Supprimez la confirmation ou saisissez un nouveau mot de passe.",
                    new[] { nameof(ConfirmNewPassword) });
            yield break;
        }

        if (NewPassword.Length < 4)
            yield return new ValidationResult("Au moins 4 caractères.", new[] { nameof(NewPassword) });

        if (!string.Equals(NewPassword, ConfirmNewPassword, StringComparison.Ordinal))
            yield return new ValidationResult(
                "La confirmation ne correspond pas au nouveau mot de passe.",
                new[] { nameof(ConfirmNewPassword) });
    }
}
