using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetTopByPositions;

public record GetTopDepartmentsQuery(string? SortDirection) : IQuery;
