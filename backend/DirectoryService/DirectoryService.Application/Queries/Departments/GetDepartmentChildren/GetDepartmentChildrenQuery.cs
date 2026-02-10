using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetDepartmentChildren;

public record GetDepartmentChildrenQuery (Guid ParentId, GetDepartmentChildrenRequest Request) : IQuery;
