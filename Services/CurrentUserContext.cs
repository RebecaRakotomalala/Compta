namespace dadaApp.Services;

public class CurrentUserContext : ICurrentUserContext
{
    public const string SessionUserIdKey = "UserId";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId => _httpContextAccessor.HttpContext?.Session.GetInt32(SessionUserIdKey);

    public int GetRequiredUserId()
    {
        var id = UserId;
        if (!id.HasValue)
            throw new InvalidOperationException("Aucun utilisateur en session.");
        return id.Value;
    }
}
