using User.DAL.Models;
using User.Entities;

namespace User.DAL.Mappings;

/// <summary>Converts between table models (DAL) and business entities (BLL).</summary>
internal static class UserMappings
{
    public static UserPreferences ToEntity(this TUserPreferences m) => new()
    {
        UserId = m.UserId,
        Theme = m.Theme,
        UpdatedAtUtc = m.UpdatedAtUtc
    };

    public static UserProfile ToEntity(this TUserProfile m) => new()
    {
        UserId = m.UserId,
        Handle = m.Handle,
        DisplayName = m.DisplayName,
        Bio = m.Bio,
        JoinedAtUtc = m.JoinedAtUtc
    };

    public static TUserProfile ToModel(this UserProfile e) => new()
    {
        UserId = e.UserId,
        Handle = e.Handle,
        DisplayName = e.DisplayName,
        Bio = e.Bio,
        JoinedAtUtc = e.JoinedAtUtc
    };
}
