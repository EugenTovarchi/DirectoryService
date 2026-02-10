namespace DirectoryService.Application.Queries.Locations.Get;

public record GetLocationsRequest(
    List<Guid>?DepartmentsIds,
    string? Search,
    bool? IsActive,
    string? SortBy,
    string? SortDirection,
    int Page,
    int PageSize)
{
    public GetLocationsQuery ToQuery() => new (DepartmentsIds, Search, IsActive, SortBy, SortDirection, Page, PageSize);
}

