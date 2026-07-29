namespace FileService.Contracts.HttpCommunication;

public record FileServiceOptions
{
    public string Url { get; init; } = string.Empty;
    public string GrpcUrl { get; init; } = string.Empty;
    public string AuthServiceUrl { get; init; } = string.Empty;
    public string ServiceClientId { get; init; } = string.Empty;
    public string ServiceClientSecret { get; init; } = string.Empty;
    public string ServiceTokenScope { get; init; } = "files";
    public int TimeoutSeconds { get; init; } = 10;
}
