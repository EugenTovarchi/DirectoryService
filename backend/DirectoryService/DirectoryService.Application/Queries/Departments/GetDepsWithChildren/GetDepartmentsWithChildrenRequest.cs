namespace DirectoryService.Application.Queries.Departments.GetDepsWithChildren;

public record GetDepartmentsWithChildrenRequest(
    int? RootLimit,
    int? ChildLimit,
    int Page,
    int PageSize)
{
    public GetDepartmentsWithChildrenQuery ToQuery() => new(RootLimit, ChildLimit, Page, PageSize);
}
