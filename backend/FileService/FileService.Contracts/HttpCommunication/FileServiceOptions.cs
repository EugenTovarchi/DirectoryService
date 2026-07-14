namespace FileService.Contracts.HttpCommunication;

public record FileServiceOptions
{
    public string Url { get; init; } = string.Empty;
    public string GrpcUrl { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 10;
}
