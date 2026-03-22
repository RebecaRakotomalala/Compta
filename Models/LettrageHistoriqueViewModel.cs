namespace dadaApp.Models
{
    /// <summary>
    /// Ligne d'historique de lettrage manuel avec état d'application sur les écritures courantes.
    /// </summary>
    public class LettrageHistoriqueViewModel
    {
        public LettrageManuel Lettrage { get; set; } = null!;
        /// <summary>Toutes les écritures portent ce numéro de lettrage et leur nombre correspond au snapshot JSON.</summary>
        public bool EstApplique { get; set; }
        /// <summary>Nombre de lignes dans le snapshot JSON (0 si JSON invalide).</summary>
        public int NombreLignesSnapshot { get; set; }
    }
}
