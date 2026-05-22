namespace AuthService.Core.Options;

/// <summary>
/// Настройки выпуска и проверки JWT. Реальный SigningKey хранится только в User Secrets, .env или secret manager.
/// </summary>
public sealed class JwtOptions
{
    public const string SECTION_NAME = "Jwt";
    public const int MIN_SIGNING_KEY_LENGTH = 32;

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; init; }
    public int RefreshTokenLifetimeDays { get; init; }
}
