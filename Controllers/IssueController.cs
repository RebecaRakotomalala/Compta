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
    public async Task<IActionResult> Index(string? client)
    {
        var dashboard = await _grandLivreService.GetIssueDashboardParSemainesAsync(client);

        var allClientNames = await _grandLivreService.GetAllClientNamesAsync();

        ViewBag.ClientFilter = client ?? string.Empty;
        ViewBag.AllClientNames = allClientNames;

        return View(dashboard);
    }
}

