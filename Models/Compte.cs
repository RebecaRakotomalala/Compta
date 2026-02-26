// Models/Compte.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dadaApp.Models
{
    [Table("comptes")]
    public class Compte
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("compte_id")]
        public int CompteId { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("numero_compte")]
        public string NumeroCompte { get; set; } = string.Empty;

        [MaxLength(20)]
        [Column("code_client")]
        public string? CodeClient { get; set; }

        [Required]
        [MaxLength(200)]
        [Column("nom_client")]
        public string NomClient { get; set; } = string.Empty;

        [Column("date_creation")]
        public DateTime DateCreation { get; set; }

        public ICollection<Ecriture> Ecritures { get; set; } = new List<Ecriture>();
    }
}