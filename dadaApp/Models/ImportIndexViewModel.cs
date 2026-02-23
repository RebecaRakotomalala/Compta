namespace dadaApp.Models
{
    public class ImportIndexViewModel
    {
        public int NombreLignesImportees { get; set; }
        public int NombreComptes { get; set; }
        public int NombreEcritures { get; set; }
        public List<string> Erreurs { get; set; } = new();
        public string? MessageSuccess { get; set; }
        public string? MessageError { get; set; }
    }
}