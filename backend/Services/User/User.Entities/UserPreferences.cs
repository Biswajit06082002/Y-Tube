namespace User.Entities;

/// <summary>
/// Per-user settings. Keyed by the user id from the JWT ("sub") — the User service never reads
/// Auth's tables; the id in the token is the only link between the two databases.
/// </summary>
public class UserPreferences
{
    public Guid UserId { get; set; }
    public string Theme { get; set; } = "system";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
