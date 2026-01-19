using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Commands.Departments.GetTopByPositions;

public record GetTopDepartmentsQuery(string? SortDirection) : IQuery;
