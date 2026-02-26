// Models/VueSoldeCompte.cs (pour la vue)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace dadaApp.Models
{
    [Table("VueSoldesComptes")]
    [Keyless] // Les vues n'ont pas de clé primaire
    public class VueSoldeCompte
    {
        [Column("CompteId")]
        public int CompteId { get; set; }
        
        [Column("NumeroCompte")]
        public string NumeroCompte { get; set; } = string.Empty;
        
        [Column("NomClient")]
        public string NomClient { get; set; } = string.Empty;
        
        [Column("TotalDebit")]
        public decimal? TotalDebit { get; set; }
        
        [Column("TotalCredit")]
        public decimal? TotalCredit { get; set; }
        
        [Column("Solde")]
        public decimal? Solde { get; set; }
    }
}