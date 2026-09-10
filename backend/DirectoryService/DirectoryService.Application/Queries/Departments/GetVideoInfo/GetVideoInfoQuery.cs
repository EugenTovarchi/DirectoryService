using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetVideoInfo;

public record GetVideoInfoQuery(Guid DepartmentId) : IQuery;