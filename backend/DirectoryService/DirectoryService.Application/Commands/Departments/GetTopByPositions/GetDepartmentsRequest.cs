namespace DirectoryService.Application.Commands.Departments.GetTopByPositions;

public record GetDepartmentsRequest(string? SortDirection)
{
    public GetTopDepartmentsQuery ToQuery() => new(SortDirection);
}
