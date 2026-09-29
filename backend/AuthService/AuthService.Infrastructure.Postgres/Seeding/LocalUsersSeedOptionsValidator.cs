using AuthService.Domain.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Postgres.Seeding;

/// <summary>
/// Проверяет local user seed до запуска приложения.
/// В сообщениях намеренно нет password values.
/// </summary>
public sealed class LocalUsersSeedOptionsValidator : IValidateOptions<LocalUsersSeedOptions>
{
    private readonly IHostEnvironment _environment;

    public LocalUsersSeedOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Проверяет environment, обязательные поля, известные роли и уникальность email.
    /// </summary>
    public ValidateOptionsResult Validate(string? name, LocalUsersSeedOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (!_environment.IsDevelopment() &&
            !_environment.IsEnvironment("Docker") &&
            !_environment.IsEnvironment("Testing"))
        {
            return ValidateOptionsResult.Fail(
                "LocalUsersSeed is allowed only in Development, Docker or Testing environment");
        }

        if (options.Users.Count == 0)
        {
            return ValidateOptionsResult.Fail(
                "LocalUsersSeed:Users must contain at least one user when seed is enabled");
        }

        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (LocalUserSeedDefinition user in options.Users)
        {
            if (string.IsNullOrWhiteSpace(user.Email) ||
                string.IsNullOrWhiteSpace(user.DisplayName) ||
                string.IsNullOrWhiteSpace(user.Role))
            {
                return ValidateOptionsResult.Fail(
                    "Every LocalUsersSeed user requires Email, DisplayName and Role");
            }

            if (string.IsNullOrWhiteSpace(user.Password) &&
                string.IsNullOrWhiteSpace(options.Password))
            {
                return ValidateOptionsResult.Fail(
                    "LocalUsersSeed requires a shared Password or an individual user Password");
            }

            if (!IsKnownRole(user.Role))
            {
                return ValidateOptionsResult.Fail(
                    $"LocalUsersSeed contains unsupported role '{user.Role}'");
            }

            if (!emails.Add(user.Email.Trim()))
            {
                return ValidateOptionsResult.Fail(
                    "LocalUsersSeed contains duplicate user email");
            }
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsKnownRole(string role)
    {
        return role == AuthRoles.SYSTEM_ADMIN ||
            role == AuthRoles.COMPANY_ADMIN ||
            role == AuthRoles.OPERATOR ||
            role == AuthRoles.TECHNICIAN ||
            role == AuthRoles.VIEWER;
    }
}
