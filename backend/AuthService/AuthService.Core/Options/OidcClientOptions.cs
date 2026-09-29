namespace AuthService.Core.Options;

/// <summary>
/// Описание одного OAuth/OIDC client для idempotent seed.
/// Client без secret считается public; client с secret — confidential.
/// </summary>
public sealed class OidcClientOptions
{
    public const int MIN_CLIENT_SECRET_LENGTH = 32;

    public string ClientId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Secret нужен только confidential client и должен приходить из runtime secrets.
    /// Public clients, например Postman или SPA, оставляют это поле пустым и используют PKCE.
    /// </summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// OpenIddict вернёт authorization code только на один из заранее разрешённых адресов.
    /// </summary>
    public List<string> RedirectUris { get; init; } = new();

    public List<string> AllowedScopes { get; init; } = new();
}
