using Microsoft.AspNetCore.Mvc;
using dadaApp.Models;
using dadaApp.Services;
using dadaApp.Data;
using Microsoft.EntityFrameworkCore;

namespace dadaApp.Controllers;

public class ImportController : Controller
{
    private readonly ILogger<ImportController> _logger;
    private readonly ImportComptabiliteService _importService;
    private readonly AppDbContext _context;

    public ImportController(
        ILogger<ImportController> logger,
        ImportComptabiliteService importService,
        AppDbContext context)
    {
        _logger = logger;
        _importService = importService;
        _context = context;
    }

    public IActionResult Index()
    {
        var vm = new ImportIndexViewModel();
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFileCollection files)
    {
        var vm = new ImportIndexViewModel();

        try
        {
            if (files == null || files.Count == 0)
            {
                vm.MessageError = "Veuillez sélectionner au moins un fichier";
                return View("Index", vm);
            }

            // Sauvegarder le dossier des uploads
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            int totalLinesImported = 0;
            var totalErrors = new List<string>();
            var successCount = 0;

            // Traiter chaque fichier
            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                    continue;

                try
                {
                    var filePath = Path.Combine(uploadsFolder, $"{DateTime.Now:yyyyMMddHHmmss}_{file.FileName}");
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Importer via le service
                    using var fileStream = file.OpenReadStream();
                    var resultat = await _importService.ImporterDepuisCsv(fileStream, file.FileName);

                    if (resultat.Succes)
                    {
                        totalLinesImported += resultat.NombreLignesImportees;
                        successCount++;
                    }
                    else
                    {
                        totalErrors.AddRange(resultat.Erreurs);
                    }
                }
                catch (Exception ex)
                {
                    totalErrors.Add($"Erreur lors du traitement de {file.FileName}: {ex.Message}");
                }
            }

            // Préparer le message de résultat
            if (successCount > 0)
            {
                vm.MessageSuccess = $"✅ Import réussi ! {successCount} fichier(s) traité(s), {totalLinesImported} lignes importées.";
            }

            if (totalErrors.Count > 0)
            {
                vm.MessageError = "⚠️ Certains fichiers n'ont pas pu être importés:\n" + string.Join("\n", totalErrors);
            }

            if (successCount == 0 && totalErrors.Count == 0)
            {
                vm.MessageError = "Aucun fichier valide n'a été traité";
            }

            vm.NombreLignesImportees = totalLinesImported;
            vm.NombreComptes = await _context.Comptes.CountAsync();
            vm.NombreEcritures = await _context.Ecritures.CountAsync();
            vm.Erreurs = totalErrors;

            return View("Index", vm);
        }
        catch (Exception ex)
        {
            vm.MessageError = $"Erreur : {ex.Message}";
            return View("Index", vm);
        }
    }

    public async Task<IActionResult> Comptes()
    {
        var comptes = await _context.Comptes
            .Include(c => c.Ecritures)
            .OrderBy(c => c.NumeroCompte)
            .ToListAsync();
        return View(comptes);
    }

    public async Task<IActionResult> Soldes()
    {
        var soldes = await _context.VueSoldesComptes
            .OrderBy(s => s.NumeroCompte)
            .ToListAsync();
        return View(soldes);
    }

    public async Task<IActionResult> Ecritures(int? compteId, DateTime? dateDebut, DateTime? dateFin)
    {
        var query = _context.Ecritures.Include(e => e.Compte).AsQueryable();

        if (compteId.HasValue)
            query = query.Where(e => e.CompteId == compteId.Value);
        if (dateDebut.HasValue)
            query = query.Where(e => e.DateComptable >= dateDebut.Value);
        if (dateFin.HasValue)
            query = query.Where(e => e.DateComptable <= dateFin.Value);

        var ecritures = await query
            .OrderByDescending(e => e.DateComptable)
            .Take(100)
            .ToListAsync();

        ViewBag.Comptes = await _context.Comptes.OrderBy(c => c.NomClient).ToListAsync();
        return View(ecritures);
    }

    [HttpPost]
    public async Task<IActionResult> SupprimerTout()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Ecritures\" CASCADE");
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Comptes\" CASCADE");
            TempData["Success"] = "Données supprimées";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur suppression");
            TempData["Error"] = $"Erreur : {ex.Message}";
        }
        return RedirectToAction("Index");
    }
}