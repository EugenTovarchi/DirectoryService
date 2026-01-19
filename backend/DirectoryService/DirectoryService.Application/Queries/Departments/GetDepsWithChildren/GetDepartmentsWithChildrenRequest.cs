namespace DirectoryService.Application.Queries.Departments.GetDepsWithChildren;

public record GetDepartmentsWithChildrenRequest(
    int? RootLimit,
    int? ChildLimit,
    string? SortBy,
    string? SortDirection,
    int Page,
    int PageSize)
{
    public GetDepartmentsWithChildrenQuery ToQuery() => new(RootLimit, ChildLimit, SortBy, SortDirection, Page, PageSize);
}
