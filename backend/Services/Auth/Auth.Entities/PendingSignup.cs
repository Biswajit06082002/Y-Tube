namespace Auth.Entities;

/// <summary>
/// A sign-up waiting for email verification. Lives only in Redis with a TTL,
/// so abandoned sign-ups clean themselves up and never touch SQL Server.
/// </summary>
public sealed class PendingSignup
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string OtpHash { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }
    public int ResendCount { get; set; }
}
