namespace YTube.Common.Responses;

/// <summary>
/// One page of a list. Offset paging (page/pageSize) for now; a feed that changes while
/// you scroll is better served by cursor paging, which we'll switch to in the scale phase.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public bool HasMore => Page * PageSize < TotalCount;

    public static PagedResult<T> Empty(int page, int pageSize) => new([], page, pageSize, 0);
}
