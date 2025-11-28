namespace DirectoryService.SharedKernel.ValueObjects;

public record DepartmentPathInfo(Path Path, short Depth, Guid? ParentId);