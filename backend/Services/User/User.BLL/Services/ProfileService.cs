using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using User.BLL.Interfaces;
using User.Entities;
using User.Entities.Contracts;
using YTube.Common.Messaging;
using YTube.Common.Results;
using YTube.Contracts.Events;

namespace User.BLL.Services;

/// <summary>Profiles (channel identity) and the follow graph.</summary>
public sealed partial class ProfileService(
    IProfileRepository profiles,
    IFollowRepository follows,
    IMessagePublisher publisher,
    ILogger<ProfileService> logger) : IProfileService
{
    private const int MaxDisplayName = 50;
    private const int MaxBio = 1000;

    [GeneratedRegex("^[a-z0-9_.]{3,30}$")]
    private static partial Regex HandlePattern();

    [GeneratedRegex("[^a-z0-9_.]")]
    private static partial Regex HandleInvalidChars();

    public async Task<ProfileDto?> GetAsync(Guid userId, Guid? viewerId, CancellationToken ct = default)
    {
        var profile = await profiles.GetAsync(userId, ct);
        return profile is null ? null : await ToDtoAsync(profile, viewerId, ct);
    }

    public async Task<ProfileDto> GetOrCreateMeAsync(Guid userId, string? email, CancellationToken ct = default)
    {
        await EnsureProfileAsync(userId, email, null, ct);
        return (await GetAsync(userId, userId, ct))!;
    }

    public async Task EnsureProfileAsync(Guid userId, string? email, DateTime? joinedAtUtc = null, CancellationToken ct = default)
    {
        if (await profiles.GetAsync(userId, ct) is not null) return;

        var localPart = (email ?? "creator").Split('@')[0];
        var baseHandle = HandleInvalidChars().Replace(localPart.ToLowerInvariant(), "");
        if (baseHandle.Length < 3) baseHandle = "creator";
        if (baseHandle.Length > 24) baseHandle = baseHandle[..24];

        // Try the plain handle first, then add a random suffix if someone already has it.
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var handle = attempt == 0 ? baseHandle : $"{baseHandle}{RandomNumberGenerator.GetInt32(1000, 10000)}";
            if (await profiles.HandleTakenAsync(handle, userId, ct)) continue;

            var created = await profiles.TryAddAsync(new UserProfile
            {
                UserId = userId,
                Handle = handle,
                DisplayName = localPart.Length > MaxDisplayName ? localPart[..MaxDisplayName] : localPart,
                JoinedAtUtc = joinedAtUtc ?? DateTime.UtcNow
            }, ct);

            // Lost a race (event consumer and a request at the same moment)? Fine if the profile now exists.
            if (created || await profiles.GetAsync(userId, ct) is not null) return;
        }

        throw new InvalidOperationException($"Could not create a profile for {userId}.");
    }

    public async Task<Result<ProfileDto>> UpdateMeAsync(Guid userId, string? email, UpdateProfileRequest request, CancellationToken ct = default)
    {
        await EnsureProfileAsync(userId, email, null, ct);
        var profile = (await profiles.GetAsync(userId, ct))!;
        var before = (profile.DisplayName, profile.Handle);

        if (request.DisplayName is not null)
        {
            var name = request.DisplayName.Trim();
            if (name.Length == 0) return Error.Validation("name_required", "Please enter a name.");
            if (name.Length > MaxDisplayName) return Error.Validation("name_too_long", $"Names can be at most {MaxDisplayName} characters.");
            profile.DisplayName = name;
        }

        if (request.Handle is not null)
        {
            var handle = request.Handle.Trim().TrimStart('@').ToLowerInvariant();
            if (!HandlePattern().IsMatch(handle))
                return Error.Validation("handle_invalid", "Handles are 3–30 characters: letters, numbers, '_' and '.'.");
            if (handle != profile.Handle && await profiles.HandleTakenAsync(handle, userId, ct))
                return Error.Conflict("handle_taken", $"@{handle} is already taken.");
            profile.Handle = handle;
        }

        if (request.Bio is not null)
        {
            var bio = request.Bio.Trim();
            if (bio.Length > MaxBio) return Error.Validation("bio_too_long", $"The description can be at most {MaxBio} characters.");
            profile.Bio = bio;
        }

        if (!await profiles.TryUpdateAsync(profile, ct))
            return Error.Conflict("handle_taken", $"@{profile.Handle} is already taken.");

        // Other services keep a copy of the public name; tell them it changed (eventual consistency).
        if (before != (profile.DisplayName, profile.Handle))
        {
            try
            {
                await publisher.PublishAsync(UserRoutingKeys.ProfileUpdated, new UserProfileUpdated(userId, profile.DisplayName, profile.Handle), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not publish UserProfileUpdated for {UserId}", userId);
            }
        }

        return await ToDtoAsync(profile, userId, ct);
    }

    public async Task<Result<ProfileDto>> SetFollowAsync(Guid followerId, Guid followeeId, bool follow, CancellationToken ct = default)
    {
        if (followerId == followeeId)
            return Error.Validation("cannot_follow_self", "You can't follow yourself.");

        var followee = await profiles.GetAsync(followeeId, ct);
        if (followee is null)
            return Error.NotFound("channel_not_found", "This channel doesn't exist.");

        await follows.SetFollowAsync(followerId, followeeId, follow, ct);
        return await ToDtoAsync(followee, followerId, ct);
    }

    public async Task<IReadOnlyList<ProfileDto>> GetFollowingAsync(Guid userId, CancellationToken ct = default)
    {
        var ids = await follows.GetFolloweeIdsAsync(userId, ct);
        var list = await profiles.GetManyAsync(ids, ct);

        var result = new List<ProfileDto>();
        foreach (var p in list.OrderBy(p => p.DisplayName))
            result.Add(await ToDtoAsync(p, userId, ct));
        return result;
    }

    public Task<IReadOnlyList<Guid>> GetFollowingIdsAsync(Guid userId, CancellationToken ct = default) =>
        follows.GetFolloweeIdsAsync(userId, ct);

    private async Task<ProfileDto> ToDtoAsync(UserProfile p, Guid? viewerId, CancellationToken ct) => new(
        p.UserId, p.Handle, p.DisplayName, p.Bio, p.JoinedAtUtc,
        await follows.CountFollowersAsync(p.UserId, ct),
        await follows.CountFollowingAsync(p.UserId, ct),
        viewerId is { } v && v != p.UserId && await follows.IsFollowingAsync(v, p.UserId, ct),
        viewerId == p.UserId);
}
