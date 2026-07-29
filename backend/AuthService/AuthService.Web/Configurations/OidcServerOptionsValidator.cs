using AuthService.Core.Options;
using Microsoft.Extensions.Options;

namespace AuthService.Web.Configurations;

/// <summary>
/// Останавливает приложение при небезопасной или неполной OIDC configuration.
/// В production нельзя незаметно перейти на development certificates или HTTP issuer.
/// </summary>
public sealed class OidcServerOptionsValidator : IValidateOptions<OidcServerOptions>
{
    private readonly IHostEnvironment _environment;

    public OidcServerOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Проверяет protocol lifetime, issuer и выбранный способ загрузки certificates.
    /// Testing использует отдельные ephemeral keys, поэтому paths ему не требуются.
    /// </summary>
    public ValidateOptionsResult Validate(string? name, OidcServerOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        if (!Uri.TryCreate(options.Issuer, UriKind.Absolute, out Uri? issuer) ||
            !string.Equals(issuer.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail("Oidc:Issuer must be an absolute HTTPS URI");
        }

        if (options.AccessTokenLifetimeMinutes <= 0)
            return ValidateOptionsResult.Fail("Oidc:AccessTokenLifetimeMinutes must be positive");

        if (options.RefreshTokenLifetimeDays <= 0)
            return ValidateOptionsResult.Fail("Oidc:RefreshTokenLifetimeDays must be positive");

        var clientIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (OidcClientOptions client in options.Clients)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId))
                return ValidateOptionsResult.Fail("Each Oidc client must have ClientId");

            if (!clientIds.Add(client.ClientId))
                return ValidateOptionsResult.Fail($"Oidc client '{client.ClientId}' is duplicated");

            if (string.IsNullOrWhiteSpace(client.DisplayName))
                return ValidateOptionsResult.Fail($"Oidc client '{client.ClientId}' must have DisplayName");

            if (!string.IsNullOrEmpty(client.ClientSecret) &&
                client.ClientSecret.Length < OidcClientOptions.MIN_CLIENT_SECRET_LENGTH)
            {
                return ValidateOptionsResult.Fail(
                    $"Oidc client '{client.ClientId}' secret must have at least " +
                    $"{OidcClientOptions.MIN_CLIENT_SECRET_LENGTH} characters");
            }

            if (client.RedirectUris.Count == 0 ||
                client.RedirectUris.Any(uri => !Uri.TryCreate(uri, UriKind.Absolute, out _)))
            {
                return ValidateOptionsResult.Fail(
                    $"Oidc client '{client.ClientId}' must have valid absolute RedirectUris");
            }

            if (client.AllowedScopes.Count == 0 ||
                client.AllowedScopes.Any(scope => !OidcScopes.All.Contains(scope)))
            {
                return ValidateOptionsResult.Fail(
                    $"Oidc client '{client.ClientId}' contains missing or unsupported AllowedScopes");
            }
        }

        if (_environment.IsEnvironment("Testing"))
            return ValidateOptionsResult.Success;

        bool allowsDevelopmentCertificates =
            _environment.IsDevelopment() || _environment.IsEnvironment("Docker");

        if (options.DisableTransportSecurityRequirement &&
            !allowsDevelopmentCertificates &&
            !_environment.IsEnvironment("Testing"))
        {
            return ValidateOptionsResult.Fail(
                "Oidc:DisableTransportSecurityRequirement is allowed only in Development, Docker or Testing");
        }

        if (options.UseDevelopmentCertificates)
        {
            return allowsDevelopmentCertificates
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(
                    "Oidc:UseDevelopmentCertificates is allowed only in Development or Docker");
        }

        if (string.IsNullOrWhiteSpace(options.SigningCertificatePath))
            return ValidateOptionsResult.Fail("Oidc:SigningCertificatePath is required");

        if (string.IsNullOrWhiteSpace(options.EncryptionCertificatePath))
            return ValidateOptionsResult.Fail("Oidc:EncryptionCertificatePath is required");

        return ValidateOptionsResult.Success;
    }
}
