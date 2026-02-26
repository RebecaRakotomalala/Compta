// Models/Ecriture.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dadaApp.Models
{
    [Table("ecritures")]
    public class Ecriture
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ecriture_id")]
        public int EcritureId { get; set; }

        [Required]
        [Column("compte_id")]
        public int CompteId { get; set; }

        [Required]
        [Column("date_comptable")]
        public DateTime DateComptable { get; set; }

        [Column("numero_piece")]
        public string? NumeroPiece { get; set; }

        [Column("code_journal")]
        public string? CodeJournal { get; set; }

        [Column("libelle")]
        public string? Libelle { get; set; }

        [Column("date_facture")]
        public DateTime? DateFacture { get; set; }

        [Column("reference")]
        public string? Reference { get; set; }

        [Column("date_echeance")]
        public DateTime? DateEcheance { get; set; }

        [Column("debit", TypeName = "decimal(15,2)")]
        public decimal Debit { get; set; }

        [Column("credit", TypeName = "decimal(15,2)")]
        public decimal Credit { get; set; }

        [Column("numero_lettrage")]
        public string? NumeroLettrage { get; set; }

        [Column("date_saisie")]
        public DateTime DateSaisie { get; set; }

        [ForeignKey("CompteId")]
        public Compte Compte { get; set; } = null!;
    }
}