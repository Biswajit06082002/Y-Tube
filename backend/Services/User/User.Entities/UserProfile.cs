namespace User.Entities;

/// <summary>A user's public channel identity. Keyed by the same id as the Auth account (JWT "sub").</summary>
public class UserProfile
{
    public Guid UserId { get; set; }

    /// <summary>Unique, URL-friendly name shown as @handle (lower-case letters, digits, '_' and '.').</summary>
    public string Handle { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public DateTime JoinedAtUtc { get; set; }
}
