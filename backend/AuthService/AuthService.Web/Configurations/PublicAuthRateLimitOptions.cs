namespace AuthService.Web.Configurations;

/// <summary>
/// Fixed-window (фиксированное временное окно) лимиты для public auth endpoints.
/// Они защищают публичные входы от перебора паролей и массовых повторных запросов;
/// блокировка входа для конкретного пользователя настраивается отдельно в Identity.
/// </summary>
public sealed class PublicAuthRateLimitOptions
{
    public const string SECTION_NAME = "PublicAuthRateLimits";

    public bool Enabled { get; set; } = true;
    public int WindowSeconds { get; set; } = 60;
    public int LoginPermitLimit { get; set; } = 10;
    public int RefreshPermitLimit { get; set; } = 30;
    public int PasswordResetPermitLimit { get; set; } = 3;
    public int InviteResendPermitLimit { get; set; } = 10;
}
