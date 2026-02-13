namespace DirectoryService.Contracts.ValueObjects;

public record DepartmentPathInfo(Path Path, short Depth, Guid? ParentId);