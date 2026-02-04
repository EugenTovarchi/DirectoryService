using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetDepsWithChildren;

public record GetDepartmentsWithChildrenQuery(
    int? RootLimit,
    int? ChildLimit,
    int Page,
    int PageSize): IQuery;
