namespace DirectoryService.Contracts.Responses;

public class GetLocationResponse
{
    public string Name { get; init; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string House { get; set; } = string.Empty;
    public int? Flat { get; set; }
    public string TimeZone {  get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
}
