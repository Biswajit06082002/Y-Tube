namespace User.DAL.Models;

/// <summary>
/// One edge of the follow graph (Follows table): FollowerId follows FolloweeId.
/// Primary key (FollowerId, FolloweeId) makes a double follow impossible.
/// </summary>
public class TFollow
{
    public Guid FollowerId { get; set; }
    public Guid FolloweeId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
