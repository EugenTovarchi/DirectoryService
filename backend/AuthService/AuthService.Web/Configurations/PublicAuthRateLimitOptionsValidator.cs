using Microsoft.Extensions.Options;

namespace AuthService.Web.Configurations;

/// <summary>
/// Проверяет fixed-window rate limits до регистрации первых public auth requests.
/// </summary>
public sealed class PublicAuthRateLimitOptionsValidator : IValidateOptions<PublicAuthRateLimitOptions>
{
    public ValidateOptionsResult Validate(string? name, PublicAuthRateLimitOptions options)
    {
        List<string> failures = [];

        if (options.WindowSeconds <= 0)
            failures.Add("PublicAuthRateLimits:WindowSeconds must be positive");

        if (options.LoginPermitLimit <= 0)
            failures.Add("PublicAuthRateLimits:LoginPermitLimit must be positive");

        if (options.RefreshPermitLimit <= 0)
            failures.Add("PublicAuthRateLimits:RefreshPermitLimit must be positive");

        if (options.PasswordResetPermitLimit <= 0)
            failures.Add("PublicAuthRateLimits:PasswordResetPermitLimit must be positive");

        if (options.InviteResendPermitLimit <= 0)
            failures.Add("PublicAuthRateLimits:InviteResendPermitLimit must be positive");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
