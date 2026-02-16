namespace DirectoryService.Contracts.Responses;

public record DepartmentResponse
{
    public Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Identifier { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public short Depth { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool HasMoreChildren { get; init; }

    public List<DepartmentResponse> Children { get; init; } = [];
}
