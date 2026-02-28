namespace FileService.Infrastructure.S3;

public record S3Options
{
    public string Endpoint { get; init; }

    public string AccessKey { get; init; }

    public string SecretKey { get; init; }

    public IReadOnlyList<string> RequiredBuckets { get; init; } = [];

    public int UploadUrlExpirationMinutes { get; init; } = 10;

    public int UploadUrlExpirationHours { get; init; } = 1;

    public int DownloadUrlExpirationDays { get; init; } = 5;

    public int MaxConcurrentRequests { get; init; } = 50;

    public bool WithSsl { get; init; }

    public long RecommendedChunkSizeBytes { get; init; } = 100 * 1024 * 1024; // 100 Mb.

    public int MaxChunks { get; init; } = 100;
}