using Microsoft.AspNetCore.Mvc;
using dadaApp.Services;
using dadaApp.Models;

namespace dadaApp.Controllers
{
    public class GrandlivreController : Controller
    {
        private readonly GrandLivreService _grandLivreService;

        public GrandlivreController(GrandLivreService grandLivreService)
        {
            _grandLivreService = grandLivreService; 
        }

        public async Task<IActionResult> Listes(
            string? client,
            string? filterType,
            int page = 1,
            int pageSize = 50
        )
        {
            // sécurité minimale
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 500) pageSize = 500; // optionnel (anti-crash)

            var allEcritures = await _grandLivreService.GetEcrituresAvecCompteAsync();

            ViewBag.AllClientNames = allEcritures
                .Where(e => e.Compte != null && !string.IsNullOrWhiteSpace(e.Compte.NomClient))
                .Select(e => e.Compte!.NomClient!)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            var ecritures = allEcritures.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(client))
            {
                var search = client.Trim().ToLower();

                ecritures = ecritures.Where(e =>
                    e.Compte != null &&
                    e.Compte.NomClient != null &&
                    e.Compte.NomClient.ToLower().Contains(search)
                );
            }

            filterType = string.IsNullOrWhiteSpace(filterType) ? "all" : filterType;

            if (filterType == "non-lettre")
                ecritures = ecritures.Where(e => string.IsNullOrWhiteSpace(e.NumeroLettrage));
            else if (filterType == "lettre")
                ecritures = ecritures.Where(e => !string.IsNullOrWhiteSpace(e.NumeroLettrage));

            var list = ecritures.ToList();

            // 🔹 infos pagination
            ViewBag.PageSize = pageSize;
            ViewBag.CurrentPage = page;
            ViewBag.TotalItems = list.Count;
            ViewBag.SelectedClient = client;
            ViewBag.FilterType = filterType;

            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> CreerLettrage([FromBody] List<int> ecritureIds)
        {
            var result = await _grandLivreService.CreerLettrageManuelAsync(ecritureIds);
            
            return Json(new
            {
                success = result.success,
                message = result.message,
                numeroLettrage = result.numeroLettrage
            });
        }

        [HttpPost]
        public async Task<IActionResult> SupprimerLettrage([FromBody] string numeroLettrage)
        {
            var result = await _grandLivreService.SupprimerLettrageManuelAsync(numeroLettrage);
            
            return Json(new
            {
                success = result.success,
                message = result.message
            });
        }

        [HttpPost]
        public async Task<IActionResult> SupprimerTousLesLettrages()
        {
            var result = await _grandLivreService.SupprimerTousLesLettragesAsync();
            
            return Json(new
            {
                success = result.success,
                message = result.message
            });
        }

        public async Task<IActionResult> HistoriqueLettrage()
        {
            var lettrages = await _grandLivreService.GetLettrageManuelsAsync();
            return View(lettrages);
        }

        /// <summary>
        /// Propose automatiquement des lettrages pour les écritures non lettrées
        /// où la somme des débits - somme des crédits = 0, pour le même nom client
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ProposerLettrages(string? client = null)
        {
            Console.WriteLine($"🔍 [Controller] ProposerLettrages appelé (client: {client ?? "tous"})");
            try
            {
                Console.WriteLine("🔍 [Controller] Appel du service ProposerLettragesAutomatiquesAsync...");
                var propositions = await _grandLivreService.ProposerLettragesAutomatiquesAsync(client);
                Console.WriteLine($"🔍 [Controller] Service retourné {propositions.Count} propositions");
                
                var result = new
                {
                    success = true,
                    propositions = propositions,
                    nombrePropositions = propositions.Count
                };
                
                Console.WriteLine("🔍 [Controller] Retour de la réponse JSON");
                return Json(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔍 [Controller] ERREUR: {ex.Message}");
                Console.WriteLine($"🔍 [Controller] Stack trace: {ex.StackTrace}");
                return Json(new
                {
                    success = false,
                    message = $"Erreur lors de la génération des propositions: {ex.Message}"
                });
            }
        }
    }
}
