namespace DirectoryService.Contracts.ValueObjects;

public sealed record DepartmentPathInfo(Path Path, short Depth, Guid? ParentId);
