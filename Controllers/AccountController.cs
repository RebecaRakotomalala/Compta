using Microsoft.AspNetCore.Mvc;
using dadaApp.Models;
using dadaApp.Data; // <== important
using System.Linq;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(AppDbContext context, ILogger<AccountController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Login(User model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        User? user = null;
        try
        {
            // Vérifier si l'utilisateur existe dans la base
            user = _context.Users
                .FirstOrDefault(u => u.Username == model.Username && u.Password == model.Password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la verification du login.");
            ModelState.AddModelError("", "Base de donnees indisponible ou non initialisee.");
            return View(model);
        }

        if (user != null)
        {
            // connexion réussie
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError("", "Identifiant ou mot de passe incorrect");
        return View(model);
    }
}
