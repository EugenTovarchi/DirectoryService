namespace AuthService.Web.Authentication;

/// <summary>
/// Имена Bearer schemes на время поэтапного перехода с custom HMAC JWT
/// на единый OpenIddict access token.
/// </summary>
public static class AuthServiceBearerAuthenticationDefaults
{
    /// <summary>
    /// Default scheme защищённых AuthService API.
    /// Он только выбирает validator и сам не доверяет данным из token.
    /// </summary>
    public const string TRANSITIONAL_SCHEME = "AuthServiceBearer";

    /// <summary>
    /// Временный validator tokens, выданных legacy /api/auth/login.
    /// Удаляется вместе с custom пользовательским JWT flow.
    /// </summary>
    public const string LEGACY_JWT_SCHEME = "LegacyJwt";
}
