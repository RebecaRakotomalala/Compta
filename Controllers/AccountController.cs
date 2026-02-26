using Microsoft.AspNetCore.Mvc;
using dadaApp.Models;
using dadaApp.Data; // <== important
using System.Linq;

public class AccountController : Controller
{
    private readonly AppDbContext _context;

    public AccountController(AppDbContext context)
    {
        _context = context;
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

        // ✅ Vérifier si l'utilisateur existe dans la base
        var user = _context.Users
            .FirstOrDefault(u => u.Username == model.Username && u.Password == model.Password);

        if (user != null)
        {
            // connexion réussie
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError("", "Email ou mot de passe incorrect");
        return View(model);
    }
}
