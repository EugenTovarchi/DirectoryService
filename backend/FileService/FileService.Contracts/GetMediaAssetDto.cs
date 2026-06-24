namespace FileService.Contracts;

public record GetMediaAssetDto(
    Guid Id,
    string Status,
    string AssetType,
    string? ViewUrl,
    string? DownloadUrl,
    string? ThumbnailUrl);
