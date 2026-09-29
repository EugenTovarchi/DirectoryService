namespace AuthService.Core.Options;

/// <summary>
/// Настройки OAuth 2.0/OpenID Connect authorization server.
/// ASP.NET Core Identity по-прежнему отвечает за пользователей, пароли и роли,
/// а OpenIddict использует эти данные для стандартной выдачи protocol tokens.
/// </summary>
public sealed class OidcServerOptions
{
    public const string SECTION_NAME = "Oidc";

    /// <summary>
    /// OpenIddict является единственным issuer пользовательских и service tokens.
    /// Значение false допустимо только для специальных процессов без HTTP authentication surface.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Разрешает HTTP только для local Docker/TestServer.
    /// Production authorization server всегда обязан принимать protocol requests по HTTPS.
    /// </summary>
    public bool DisableTransportSecurityRequirement { get; init; }

    /// <summary>
    /// Стабильный HTTPS identifier authorization server, который попадает в claim <c>iss</c>.
    /// Resource services должны доверять именно этому issuer.
    /// </summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>
    /// Создаёт локальные X.509 certificates средствами OpenIddict.
    /// Разрешено только для Development/Docker и не предназначено для production.
    /// </summary>
    public bool UseDevelopmentCertificates { get; init; }

    /// <summary>
    /// Путь к production PFX certificate с private key для подписи tokens.
    /// Public часть этого ключа публикуется через JWKS.
    /// </summary>
    public string SigningCertificatePath { get; init; } = string.Empty;
    public string SigningCertificatePassword { get; init; } = string.Empty;

    /// <summary>
    /// Путь к production PFX certificate для защиты authorization codes и refresh tokens.
    /// Access tokens отдельно оставляем подписанными, но не зашифрованными JWT.
    /// </summary>
    public string EncryptionCertificatePath { get; init; } = string.Empty;
    public string EncryptionCertificatePassword { get; init; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; init; } = 15;
    public int RefreshTokenLifetimeDays { get; init; } = 30;

    /// <summary>
    /// Clients, которым разрешено запускать Authorization Code Flow.
    /// Пустой список оставляет server без зарегистрированных clients.
    /// </summary>
    public List<OidcClientOptions> Clients { get; init; } = new();
}
