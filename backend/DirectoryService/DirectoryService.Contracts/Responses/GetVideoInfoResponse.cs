namespace DirectoryService.Contracts.Responses;

public record GetVideoInfoResponse(
    Guid Id,
    string FileName,
    string ContentType,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long Size,
    TimeSpan? Duration,
    int? Width,
    int? Height,
    bool? HasAudio
);