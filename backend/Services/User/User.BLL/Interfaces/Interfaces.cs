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

public interface IProfileService
{
    /// <summary>Public profile, personalised for the viewer. Null if the user has no profile.</summary>
    Task<ProfileDto?> GetAsync(Guid userId, Guid? viewerId, CancellationToken ct = default);

    /// <summary>The signed-in user's profile, created on the spot if it doesn't exist yet.</summary>
    Task<ProfileDto> GetOrCreateMeAsync(Guid userId, string? email, CancellationToken ct = default);

    Task<Result<ProfileDto>> UpdateMeAsync(Guid userId, string? email, UpdateProfileRequest request, CancellationToken ct = default);

    /// <summary>Creates a default profile if missing (from the user.registered event, or lazily). Idempotent.</summary>
    Task EnsureProfileAsync(Guid userId, string? email, DateTime? joinedAtUtc = null, CancellationToken ct = default);

    /// <summary>Follow (true) or unfollow (false). Idempotent. Returns the followee's updated profile.</summary>
    Task<Result<ProfileDto>> SetFollowAsync(Guid followerId, Guid followeeId, bool follow, CancellationToken ct = default);

    Task<IReadOnlyList<ProfileDto>> GetFollowingAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetFollowingIdsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Everyone following a creator — the Notification service fans out new-video notifications to them.</summary>
    Task<IReadOnlyList<Guid>> GetFollowerIdsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Profiles for the given @handles (unknown handles are skipped). Used to resolve @mentions.</summary>
    Task<IReadOnlyList<ProfileSummaryDto>> GetByHandlesAsync(IEnumerable<string> handles, CancellationToken ct = default);

    /// <summary>@mention autocomplete: handles or names starting with the query.</summary>
    Task<IReadOnlyList<ProfileSummaryDto>> SearchAsync(string query, int take, CancellationToken ct = default);
}

// --- Implemented in the DAL (database access) ---

public interface IPreferencesRepository
{
    Task<UserPreferences?> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Insert on first save, update afterwards.</summary>
    Task UpsertAsync(Guid userId, string theme, CancellationToken ct = default);
}

public interface IProfileRepository
{
    Task<UserProfile?> GetAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserProfile>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    Task<IReadOnlyList<UserProfile>> GetByHandlesAsync(IReadOnlyCollection<string> handles, CancellationToken ct = default);
    Task<IReadOnlyList<UserProfile>> SearchAsync(string prefix, int take, CancellationToken ct = default);
    Task<bool> HandleTakenAsync(string handle, Guid exceptUserId, CancellationToken ct = default);

    /// <summary>False when the row (or its handle) already exists — e.g. two requests created it at once.</summary>
    Task<bool> TryAddAsync(UserProfile profile, CancellationToken ct = default);

    /// <summary>False when the new handle was taken in the meantime (unique index).</summary>
    Task<bool> TryUpdateAsync(UserProfile profile, CancellationToken ct = default);
}

public interface IFollowRepository
{
    /// <summary>Returns true only when a new follow was created (so "X followed you" is sent once).</summary>
    Task<bool> SetFollowAsync(Guid followerId, Guid followeeId, bool follow, CancellationToken ct = default);
    Task<bool> IsFollowingAsync(Guid followerId, Guid followeeId, CancellationToken ct = default);
    Task<int> CountFollowersAsync(Guid userId, CancellationToken ct = default);
    Task<int> CountFollowingAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetFolloweeIdsAsync(Guid followerId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetFollowerIdsAsync(Guid followeeId, CancellationToken ct = default);
}
