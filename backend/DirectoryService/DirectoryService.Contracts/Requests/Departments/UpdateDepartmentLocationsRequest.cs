namespace DirectoryService.Contracts.Requests.Departments;

public record UpdateDepartmentLocationsRequest(IEnumerable<Guid> LocationIds);
