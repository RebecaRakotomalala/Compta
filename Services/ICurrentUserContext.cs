namespace dadaApp.Services;

public interface ICurrentUserContext
{
    int? UserId { get; }

    /// <exception cref="InvalidOperationException">Session sans utilisateur connecté.</exception>
    int GetRequiredUserId();
}
