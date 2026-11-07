namespace YTube.Contracts.Events;

public static class UserRoutingKeys
{
    public const string UserRegistered = "user.registered";
    public const string ProfileUpdated = "user.profile.updated";
    public const string UserFollowed = "user.followed";
}

/// <summary>Published by Auth when a sign-up completes. The User service creates the profile.</summary>
public sealed record UserRegistered(Guid UserId, string Email, DateTime RegisteredAtUtc);

/// <summary>
/// Published by the User service when a profile's public name changes. The Video service keeps a
/// copy of the creator's name on every video (denormalised for fast feeds) and updates it from this.
/// </summary>
public sealed record UserProfileUpdated(Guid UserId, string DisplayName, string Handle);

/// <summary>Published by the User service when someone starts following a creator (not on repeat follows).</summary>
public sealed record UserFollowed(
    Guid FollowerId,
    string FollowerName,
    string FollowerHandle,
    Guid FolloweeId,
    DateTime FollowedAtUtc);
