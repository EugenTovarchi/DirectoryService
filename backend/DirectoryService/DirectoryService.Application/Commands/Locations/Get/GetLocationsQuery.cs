using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Commands.Locations.Get;

public record GetLocationsQuery(
    List<Guid>? DepartmentsIds,
    string? Search,
    bool? IsActive,
    string? SortBy,
    string? SortDirection,
    int Page,
    int PageSize) : IQuery;
