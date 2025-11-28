namespace DirectoryService.Contracts.Requests.Positions;

public  record CreatePositionRequest(string PositionName, string? Description, IEnumerable<Guid> DepartmentIds);

