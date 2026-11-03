namespace User.DAL.Models;

/// <summary>Row of the UserProfiles table. The BLL works with User.Entities.UserProfile instead.</summary>
public class TUserProfile
{
    public Guid UserId { get; set; }
    public string Handle { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public DateTime JoinedAtUtc { get; set; }
}
