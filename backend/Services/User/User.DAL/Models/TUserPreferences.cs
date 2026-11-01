namespace User.DAL.Models;

/// <summary>Row of the UserPreferences table. The BLL works with User.Entities.UserPreferences instead.</summary>
public class TUserPreferences
{
    public Guid UserId { get; set; }
    public string Theme { get; set; } = "system";
    public DateTime UpdatedAtUtc { get; set; }
}
