using Feed.DAL.Models;
using Feed.Entities;

namespace Feed.DAL.Mappings;

internal static class FeedMappings
{
    public static FeedVideo ToEntity(this TVideoResponse m) =>
        new(m.Id, m.Title, m.Description, m.Category, m.ChannelId, m.ChannelName,
            m.Views, m.DurationSeconds, m.PublishedAtUtc, m.LikeCount, m.LikedByMe);
}
