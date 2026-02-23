namespace dadaApp.Models
{
    public class PropositionLettrage
    {
        public string NomClient { get; set; } = string.Empty;
        public string? CodeClient { get; set; }
        public List<int> EcritureIds { get; set; } = new();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public int NombreEcritures { get; set; }
        
        // Informations supplémentaires pour l'affichage
        public List<EcritureInfo> Ecritures { get; set; } = new();
    }

    public class EcritureInfo
    {
        public int EcritureId { get; set; }
        public DateTime DateComptable { get; set; }
        public string? NumeroPiece { get; set; }
        public string? Libelle { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}

