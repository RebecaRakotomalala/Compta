namespace dadaApp.Models
{
    /// <summary>Vue d’ensemble Issue : 5 colonnes semaine (S-2 à S+2).</summary>
    public class IssueDashboardViewModel
    {
        public List<IssueWeekColumnViewModel> Semaines { get; set; } = new();
    }

    public class IssueWeekColumnViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Titre { get; set; } = string.Empty;
        public string PeriodeLibelle { get; set; } = string.Empty;
        public DateTime LundiSemaine { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public List<IssueClientBlocViewModel> Clients { get; set; } = new();
    }

    public class IssueClientBlocViewModel
    {
        public string CodeClient { get; set; } = string.Empty;
        public string NomClient { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public List<IssueClientViewModel> Ecritures { get; set; } = new();
    }
}
