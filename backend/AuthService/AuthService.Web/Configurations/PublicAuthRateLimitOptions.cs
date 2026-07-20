namespace AuthService.Web.Configurations;

/// <summary>
/// Fixed-window (фиксированное временное окно) лимиты для public auth endpoints.
/// Они защищают публичные входы от перебора паролей и массовых повторных запросов;
/// блокировка входа для конкретного пользователя настраивается отдельно в Identity.
/// </summary>
public sealed class PublicAuthRateLimitOptions
{
    public const string SECTION_NAME = "PublicAuthRateLimits";

    /// <summary>Включает реальные лимиты; false сохраняет policies, но делает permit limit неограниченным.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Длина fixed window в секундах.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Число login requests на один IP за окно.</summary>
    public int LoginPermitLimit { get; set; } = 10;

    /// <summary>Число refresh requests на один IP за окно.</summary>
    public int RefreshPermitLimit { get; set; } = 30;

    /// <summary>Число password-reset requests на один IP за окно.</summary>
    public int PasswordResetPermitLimit { get; set; } = 3;

    /// <summary>Число resend-invite requests на один IP за окно.</summary>
    public int InviteResendPermitLimit { get; set; } = 10;
}
