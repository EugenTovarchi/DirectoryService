namespace AuthService.Core.Options;

/// <summary>
/// Настройки SMTP adapter и frontend links, которые попадают в invite/password-reset письма.
/// Credentials должны приходить из runtime secrets, а не из committed appsettings.
/// </summary>
public sealed class EmailOptions
{
    public const string SECTION_NAME = "Email";

    /// <summary>Включает реальную SMTP delivery; при false sender работает как no-op для локальных сценариев.</summary>
    public bool Enabled { get; init; }

    /// <summary>Hostname SMTP provider.</summary>
    public string SmtpHost { get; init; } = string.Empty;

    /// <summary>TCP port SMTP provider.</summary>
    public int SmtpPort { get; init; } = 25;

    /// <summary>Требовать SSL/TLS при подключении.</summary>
    public bool EnableSsl { get; init; }

    /// <summary>SMTP username из runtime secret configuration.</summary>
    public string? Username { get; init; }

    /// <summary>SMTP password из runtime secret configuration.</summary>
    public string? Password { get; init; }

    /// <summary>Адрес отправителя.</summary>
    public string FromEmail { get; init; } = string.Empty;

    /// <summary>Отображаемое имя отправителя.</summary>
    public string FromName { get; init; } = "24Eye";

    /// <summary>Frontend route для принятия приглашения.</summary>
    public string InviteBaseUrl { get; init; } = string.Empty;

    /// <summary>Frontend route для установки нового пароля.</summary>
    public string PasswordResetBaseUrl { get; init; } = string.Empty;
}
