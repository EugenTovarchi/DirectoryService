namespace DirectoryService.Core.Abstractions;

public class PagedList<T>
{
    public required IReadOnlyCollection<T> Items { get; init; }
    public long TotalCount { get; init; }
    public int PageSize { get; init; }
    public int Page { get; init; }
    public bool HasNextpage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
}
