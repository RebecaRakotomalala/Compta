using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dadaApp.Models
{
    public class LettrageManuel
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string NumeroLettrage { get; set; } = string.Empty;
        
        public DateTime DateCreation { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string CodeClient { get; set; } = string.Empty;
        
        [MaxLength(255)]
        public string NomClient { get; set; } = string.Empty;
        
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        
        public string EcrituresJson { get; set; } = string.Empty;
        
        public string? Commentaire { get; set; }
    }
    
    public class EcritureLettrageDetail
    {
        public int EcritureId { get; set; }
        public DateTime DateComptable { get; set; }
        public string CodeJournal { get; set; } = string.Empty;
        public string NumeroPiece { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public DateTime? DateEcheance { get; set; }
        public string NumeroCompte { get; set; } = string.Empty;
    }
}