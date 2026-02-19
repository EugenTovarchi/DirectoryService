namespace FileService.Infrastructure.S3;

public record S3Options
{
    public string Endpoint { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public IReadOnlyList<string> RequiredBuckets { get; init; } = [];

    // Использовать ли https или нет.
    public bool WithSsl { get; init; }
}