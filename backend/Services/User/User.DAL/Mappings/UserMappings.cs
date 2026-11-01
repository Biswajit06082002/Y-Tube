using User.DAL.Models;
using User.Entities;

namespace User.DAL.Mappings;

/// <summary>Converts table models (DAL) into business entities (BLL).</summary>
internal static class UserMappings
{
    public static UserPreferences ToEntity(this TUserPreferences m) => new()
    {
        UserId = m.UserId,
        Theme = m.Theme,
        UpdatedAtUtc = m.UpdatedAtUtc
    };
}
