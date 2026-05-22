namespace AuthService.Core.Options;

public sealed class EmailOptions
{
    public const string SECTION_NAME = "Email";

    public bool Enabled { get; init; }
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 25;
    public bool EnableSsl { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "24Eye";
    public string InviteBaseUrl { get; init; } = string.Empty;
    public string PasswordResetBaseUrl { get; init; } = string.Empty;
}
