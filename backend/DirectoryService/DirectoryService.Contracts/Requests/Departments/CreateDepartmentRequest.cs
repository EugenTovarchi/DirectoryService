namespace DirectoryService.Contracts.Requests.Departments;

public record CreateDepartmentRequest(string DepartmentName, string Identifier, IEnumerable<Guid> LocationIds, Guid? ParentId);