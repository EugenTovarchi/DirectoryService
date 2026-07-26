using Microsoft.Extensions.Options;

namespace AuthService.Core.Options;

/// <summary>
/// Проверяет SMTP и frontend link settings при старте AuthService, когда email delivery включена.
/// </summary>
public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.SmtpHost))
            failures.Add("Email:SmtpHost is required when Email:Enabled is true");

        if (options.SmtpPort <= 0)
            failures.Add("Email:SmtpPort must be positive when Email:Enabled is true");

        if (string.IsNullOrWhiteSpace(options.FromEmail))
            failures.Add("Email:FromEmail is required when Email:Enabled is true");

        if (string.IsNullOrWhiteSpace(options.InviteBaseUrl))
            failures.Add("Email:InviteBaseUrl is required when Email:Enabled is true");

        if (string.IsNullOrWhiteSpace(options.PasswordResetBaseUrl))
            failures.Add("Email:PasswordResetBaseUrl is required when Email:Enabled is true");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
