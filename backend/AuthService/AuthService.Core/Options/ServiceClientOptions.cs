namespace AuthService.Core.Options;

public sealed class ServiceClientOptions
{
    public const string SECTION_NAME = "ServiceClients";
    public const int MIN_CLIENT_SECRET_LENGTH = 32;

    public IReadOnlyList<ServiceClientDefinition> Clients { get; init; } = [];
}

public sealed class ServiceClientDefinition
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public IReadOnlyList<string> ServicePermissions { get; init; } = [];
}
