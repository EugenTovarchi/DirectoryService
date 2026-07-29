using AuthService.Core.Options;
using Microsoft.Extensions.Options;

namespace AuthService.Web.Configurations;

/// <summary>
/// Проверяет machine-to-machine clients до запуска AuthService.
/// Ошибка configuration обнаруживается при старте, а не во время первого token request.
/// </summary>
public sealed class ServiceClientOptionsValidator : IValidateOptions<ServiceClientOptions>
{
    /// <summary>
    /// Проверяет уникальность client_id, длину secrets, service permissions и разрешённые API scopes.
    /// </summary>
    public ValidateOptionsResult Validate(string? name, ServiceClientOptions options)
    {
        var clientIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (ServiceClientDefinition client in options.Clients)
        {
            if (string.IsNullOrWhiteSpace(client.ClientId))
                return ValidateOptionsResult.Fail("Each service client must have ClientId");

            if (!clientIds.Add(client.ClientId))
                return ValidateOptionsResult.Fail($"Service client '{client.ClientId}' is duplicated");

            if (string.IsNullOrWhiteSpace(client.ClientSecret) ||
                client.ClientSecret.Length < ServiceClientOptions.MIN_CLIENT_SECRET_LENGTH)
            {
                return ValidateOptionsResult.Fail(
                    $"Service client '{client.ClientId}' secret must have at least " +
                    $"{ServiceClientOptions.MIN_CLIENT_SECRET_LENGTH} characters");
            }

            if (string.IsNullOrWhiteSpace(client.ServiceName))
                return ValidateOptionsResult.Fail($"Service client '{client.ClientId}' must have ServiceName");

            if (client.ServicePermissions.Count == 0 ||
                client.ServicePermissions.Any(string.IsNullOrWhiteSpace))
            {
                return ValidateOptionsResult.Fail(
                    $"Service client '{client.ClientId}' must have valid ServicePermissions");
            }

            if (client.AllowedScopes.Count == 0 ||
                client.AllowedScopes.Any(scope => !OidcScopes.IsApiScope(scope)))
            {
                return ValidateOptionsResult.Fail(
                    $"Service client '{client.ClientId}' must have valid API-only AllowedScopes");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
