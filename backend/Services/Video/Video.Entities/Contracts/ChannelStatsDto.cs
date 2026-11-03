namespace Video.Entities.Contracts;

/// <summary>Totals across a creator's public (Ready) videos.</summary>
public sealed record ChannelStatsDto(int VideoCount, long TotalViews, long TotalLikes);

/// <summary>Result of reporting a view: Counted is false when it was a repeat within the dedup window.</summary>
public sealed record ViewResultDto(bool Counted);
