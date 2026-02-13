namespace DirectoryService.Contracts.Responses;

public record GetLocationResponse
{
    public string Name { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string House { get; init; } = string.Empty;
    public int? Flat { get; init; }
    public string TimeZone { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
}
