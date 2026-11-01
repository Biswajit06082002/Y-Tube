using System.Collections.Concurrent;
using Video.BLL.Interfaces;
using Video.Entities;

namespace Video.DAL.Repositories;

/// <summary>
/// In-memory stand-in for the Video database until uploads are built (Phase 2).
/// Data is generated from a fixed seed, so ids stay the same across restarts.
/// Likes live in memory and reset on restart. Swapping this for a SQL repository later
/// only changes the DAL — the BLL depends on IVideoRepository, not on this class.
/// </summary>
public sealed class InMemoryVideoRepository : IVideoRepository
{
    private static readonly (string Category, string[] Titles)[] Seed =
    [
        ("System Design", ["Designing YouTube from scratch", "Rate limiting explained in 10 minutes", "Consistent hashing, visually", "How Netflix serves video at scale", "CAP theorem without the jargon", "Caching strategies you should know"]),
        ("Coding", ["Angular signals in 15 minutes", ".NET 10: what's new", "Clean architecture in ASP.NET Core", "EF Core performance tips", "TypeScript generics finally make sense", "Build a REST API in one sitting"]),
        ("Music", ["Lo-fi beats to code to", "Acoustic covers, live session", "Piano for deep focus — 1 hour", "Synthwave night drive mix"]),
        ("Gaming", ["Speedrun world record attempt", "Indie games you missed this year", "Building a game engine: day 1", "Retro console restoration"]),
        ("Cooking", ["15-minute weeknight biryani", "Perfect homemade naan", "Street food tour: Kolkata", "Meal prep for a busy week"]),
        ("Travel", ["Sikkim road trip on a budget", "48 hours in Tokyo", "Hidden beaches of Goa"]),
        ("Data & ML", ["NumPy crash course", "Pandas for absolute beginners", "How recommendation systems work", "Linear regression from scratch"])
    ];

    private static readonly (string Id, string Name)[] Channels =
    [
        ("ch_bytebuilder", "ByteBuilder"), ("ch_scalewise", "ScaleWise"), ("ch_dotnetdaily", "DotNet Daily"),
        ("ch_ngcraft", "NgCraft"), ("ch_chillwave", "Chillwave Studio"), ("ch_pixelquest", "PixelQuest"),
        ("ch_masalalab", "Masala Lab"), ("ch_roamfree", "Roam Free"), ("ch_datadesk", "Data Desk")
    ];

    private readonly List<VideoItem> _videos;
    private readonly Dictionary<string, VideoItem> _byId;

    // videoId -> set of userIds. ConcurrentDictionary because requests run in parallel.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _likes = new();

    public InMemoryVideoRepository()
    {
        var random = new Random(42);
        var now = DateTime.UtcNow;
        _videos = [];

        foreach (var (category, titles) in Seed)
        {
            foreach (var title in titles)
            {
                var channel = Channels[random.Next(Channels.Length)];
                var views = (long)Math.Pow(10, 2 + random.NextDouble() * 5);
                _videos.Add(new VideoItem(
                    Id: NewId(random),
                    Title: title,
                    Description: $"{title}. A sample video in {category} from {channel.Name}. " +
                                 "Real uploads and descriptions arrive with the Video service in Phase 2.",
                    Category: category,
                    ChannelId: channel.Id,
                    ChannelName: channel.Name,
                    Views: views,
                    DurationSeconds: random.Next(45, 3600),
                    PublishedAtUtc: now.AddHours(-random.Next(2, 24 * 400)),
                    BaseLikes: (int)(views * (0.01 + random.NextDouble() * 0.04))));
            }
        }

        // Shuffle once so the feed mixes categories.
        _videos = [.. _videos.OrderBy(_ => random.Next())];
        _byId = _videos.ToDictionary(v => v.Id);
    }

    public Task<IReadOnlyList<VideoItem>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VideoItem>>(_videos);

    public Task<VideoItem?> GetByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_byId.GetValueOrDefault(id));

    public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. Seed.Select(s => s.Category)]);

    public Task<int> CountLikesAsync(string videoId, CancellationToken ct = default) =>
        Task.FromResult(_likes.TryGetValue(videoId, out var users) ? users.Count : 0);

    public Task<bool> IsLikedAsync(string videoId, string userId, CancellationToken ct = default) =>
        Task.FromResult(_likes.TryGetValue(videoId, out var users) && users.ContainsKey(userId));

    public Task<IReadOnlySet<string>> GetLikedVideoIdsAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<string>>(_likes.Where(kv => kv.Value.ContainsKey(userId)).Select(kv => kv.Key).ToHashSet());

    public Task SetLikeAsync(string videoId, string userId, bool liked, CancellationToken ct = default)
    {
        var users = _likes.GetOrAdd(videoId, _ => new ConcurrentDictionary<string, byte>());
        if (liked) users.TryAdd(userId, 0);
        else users.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    private static string NewId(Random random)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        return new string(Enumerable.Range(0, 11).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
