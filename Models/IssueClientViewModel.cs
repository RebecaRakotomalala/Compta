namespace dadaApp.Models
{
    public class IssueClientViewModel
    {
        public string CodeClient { get; set; } = string.Empty;
        public string NomClient { get; set; } = string.Empty;
        public int EcritureId { get; set; }
        public DateTime DateComptable { get; set; }
        public DateTime? DateEcheance { get; set; }
        public string? NumeroPiece { get; set; }
        public string? NumeroFacture { get; set; }
        public string? Libelle { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}

