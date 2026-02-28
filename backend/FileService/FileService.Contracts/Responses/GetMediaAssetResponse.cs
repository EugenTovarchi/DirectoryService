namespace FileService.Contracts.Responses;

public record GetMediaAssetResponse(
    Guid Id,
    string Status,
    string AssetType,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? Url,
    long? Size,
    string? FileName,
    string? ContentType );