namespace DirectoryService.Application.Queries.Departments.GetTopByPositions;

public record GetDepartmentsRequest(string? SortDirection)
{
    public GetTopDepartmentsQuery ToQuery() => new(SortDirection);
}
