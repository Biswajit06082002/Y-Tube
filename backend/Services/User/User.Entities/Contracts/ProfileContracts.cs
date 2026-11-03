namespace User.Entities.Contracts;

/// <summary>A profile as a particular viewer sees it (IsFollowedByMe / IsMe depend on who asks).</summary>
public sealed record ProfileDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    string Bio,
    DateTime JoinedAtUtc,
    int FollowerCount,
    int FollowingCount,
    bool IsFollowedByMe,
    bool IsMe);

public sealed record UpdateProfileRequest(string? DisplayName, string? Handle, string? Bio);
