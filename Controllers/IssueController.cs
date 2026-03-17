using Microsoft.AspNetCore.Mvc;
using dadaApp.Services;
using dadaApp.Models;

namespace dadaApp.Controllers;

public class IssueController : Controller
{
    private readonly GrandLivreService _grandLivreService;

    public IssueController(GrandLivreService grandLivreService)
    {
        _grandLivreService = grandLivreService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string? client)
    {
        // Si aucune date de fin n'est fournie, elle sera calculée (fin de semaine) dans le service.
        var ecritures = await _grandLivreService.GetClientsNonLettrésParEcheanceAsync(startDate, endDate, client);

        // Liste des clients pour le sélecteur avec recherche
        var allClientNames = await _grandLivreService.GetAllClientNamesAsync();

        // Si aucune date de fin n'a été passée, le service a utilisé la fin de semaine actuelle.
        // On recalcule la même logique pour remplir l'UI par défaut.
        DateTime? effectiveEndForView = endDate;
        if (!effectiveEndForView.HasValue)
        {
            var today = DateTime.Today;
            int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)today.DayOfWeek + 7) % 7;
            effectiveEndForView = today.AddDays(daysUntilSunday);
        }

        ViewBag.StartDate = startDate;
        ViewBag.EndDate = effectiveEndForView;
        ViewBag.ClientFilter = client ?? string.Empty;
        ViewBag.AllClientNames = allClientNames;

        return View(ecritures);
    }
}

