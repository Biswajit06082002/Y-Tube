using User.Entities;
using User.Entities.Contracts;
using YTube.Common.Results;

namespace User.BLL.Interfaces;

// --- Implemented in the BLL (business logic) ---

public interface IPreferencesService
{
    Task<PreferencesDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task<Result<PreferencesDto>> UpdateAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken ct = default);
}

// --- Implemented in the DAL (database access) ---

public interface IPreferencesRepository
{
    Task<UserPreferences?> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Insert on first save, update afterwards.</summary>
    Task UpsertAsync(Guid userId, string theme, CancellationToken ct = default);
}
