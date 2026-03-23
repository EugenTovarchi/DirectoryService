namespace DirectoryService.Contracts.Responses;

public record GetTopDepartmentsResponse
{
    public string Name { get; init; } = string.Empty;
    public string Identifier { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public short Depth { get; init; }
    public string ParentName { get; init; } = string.Empty;
    public Guid ParentId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public MediaDto? MediaInfo { get; init; }
    public int PositionCount { get; init; }
    public int MaxPositionCount { get; init; }
}

public class MediaDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
