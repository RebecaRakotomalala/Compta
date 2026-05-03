using dadaApp.Data;
using dadaApp.Models;
using dadaApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dadaApp.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<AccountController> _logger;
    private readonly ICurrentUserContext _currentUser;

    public AccountController(
        AppDbContext context,
        ILogger<AccountController> logger,
        ICurrentUserContext currentUser)
    {
        _context = context;
        _logger = logger;
        _currentUser = currentUser;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(User model)
    {
        if (!ModelState.IsValid)
            return View(model);

        User? user = null;
        try
        {
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
            HttpContext.Session.SetInt32(CurrentUserContext.SessionUserIdKey, user.Id);
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError("", "Identifiant ou mot de passe incorrect");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var username = model.Username.Trim();
        try
        {
            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                ModelState.AddModelError(nameof(RegisterViewModel.Username), "Ce nom d’utilisateur est déjà pris.");
                return View(model);
            }

            _context.Users.Add(new User
            {
                Username = username,
                Password = model.Password
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur inscription.");
            ModelState.AddModelError("", "Impossible de créer le compte. Réessayez plus tard.");
            return View(model);
        }

        TempData["RegisterSuccess"] = "Compte créé. Vous pouvez vous connecter.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> Profil()
    {
        var userId = _currentUser.GetRequiredUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        return View(new ProfileViewModel
        {
            Username = user.Username
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profil(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = _currentUser.GetRequiredUserId();
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        if (user.Password != model.CurrentPassword)
        {
            ModelState.AddModelError(nameof(ProfileViewModel.CurrentPassword), "Mot de passe actuel incorrect.");
            return View(model);
        }

        var newUsername = model.Username.Trim();
        if (newUsername != user.Username &&
            await _context.Users.AnyAsync(u => u.Username == newUsername && u.Id != userId))
        {
            ModelState.AddModelError(nameof(ProfileViewModel.Username), "Ce nom d’utilisateur est déjà pris.");
            return View(model);
        }

        user.Username = newUsername;
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
            user.Password = model.NewPassword;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur mise à jour profil.");
            ModelState.AddModelError("", "Enregistrement impossible. Réessayez plus tard.");
            return View(model);
        }

        TempData["ProfilSuccess"] = "Vos informations ont été enregistrées.";
        return RedirectToAction(nameof(Profil));
    }

    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }
}
