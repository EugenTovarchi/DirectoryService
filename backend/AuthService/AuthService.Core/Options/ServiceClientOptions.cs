namespace AuthService.Core.Options;

/// <summary>
/// Описывает доверенные machine-to-machine clients, которым AuthService может выдать access token.
/// Client secret должен поступать из runtime environment и не должен храниться в appsettings.
/// </summary>
public sealed class ServiceClientOptions
{
    public const string SECTION_NAME = "ServiceClients";
    public const int MIN_CLIENT_SECRET_LENGTH = 32;

    public List<ServiceClientDefinition> Clients { get; init; } = new();
}

/// <summary>
/// Настройки одного сервиса, который аутентифицируется через OAuth 2.0 Client Credentials.
/// </summary>
public sealed class ServiceClientDefinition
{
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Секрет confidential client. OpenIddict сохраняет в PostgreSQL только его hash.
    /// </summary>
    public string ClientSecret { get; init; } = string.Empty;

    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Разрешения самого сервиса, например право вызывать internal gRPC API FileService.
    /// Они не являются пользовательскими permissions и выпускаются как service_permission claims.
    /// </summary>
    public List<string> ServicePermissions { get; init; } = new();

    /// <summary>
    /// API scopes, которые client имеет право запросить у /connect/token.
    /// По scopes OpenIddict также определяет audience итогового access token.
    /// </summary>
    public List<string> AllowedScopes { get; init; } = new();
}
