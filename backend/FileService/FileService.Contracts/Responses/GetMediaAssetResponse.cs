namespace FileService.Contracts.Responses;

public record GetMediaAssetResponse(
    Guid Id,
    string Status,
    string AssetType,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? ViewUrl,
    string? DownloadUrl,
    string? ThumbnailUrl,
    long? Size,
    string? FileName,
    string? ContentType );
