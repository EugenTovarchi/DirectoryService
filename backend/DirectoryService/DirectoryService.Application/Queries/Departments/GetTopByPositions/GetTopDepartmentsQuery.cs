using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetTopByPositions;

public record GetTopDepartmentsQuery(string? SortDirection) : IQuery;
