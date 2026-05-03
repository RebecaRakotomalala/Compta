using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace dadaApp.Filters;

/// <summary>
/// Redirige vers la connexion si la session ne contient pas l’identifiant utilisateur.
/// </summary>
public sealed class RequireLoginFilter : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();

        if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(action, "Login", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(action, "Register", StringComparison.OrdinalIgnoreCase)))
            return next();

        if (string.Equals(controller, "Home", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action, "Error", StringComparison.OrdinalIgnoreCase))
            return next();

        if (!context.HttpContext.Session.GetInt32(Services.CurrentUserContext.SessionUserIdKey).HasValue)
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
            return Task.CompletedTask;
        }

        return next();
    }
}
