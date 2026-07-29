using System.Security.Claims;
using AuthService.Core.Options;
using AuthService.Domain.Identity;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace AuthService.Web.Features.Oidc.Token;

/// <summary>
/// Строит service principal после того, как OpenIddict проверил client_id, client_secret,
/// grant type, endpoint permission и запрошенные scopes.
/// </summary>
public sealed class OidcTokenHandler
{
    private readonly ServiceClientOptions _serviceClientOptions;

    public OidcTokenHandler(IOptions<ServiceClientOptions> serviceClientOptions)
    {
        _serviceClientOptions = serviceClientOptions.Value;
    }

    /// <summary>
    /// Добавляет только claims сервиса. Пользовательских ролей, company_id и permissions
    /// в Client Credentials token нет, потому что пользователь в этом flow не участвует.
    /// </summary>
    public ClaimsPrincipal? CreateServicePrincipal(OpenIddictRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return null;

        ServiceClientDefinition? serviceClient = _serviceClientOptions.Clients.SingleOrDefault(
            client => string.Equals(
                client.ClientId,
                request.ClientId,
                StringComparison.Ordinal));
        if (serviceClient is null)
            return null;

        var identity = new ClaimsIdentity(
            authenticationType: "OpenIddict",
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        identity.SetClaim(OpenIddictConstants.Claims.Subject, serviceClient.ClientId);
        identity.SetClaim(AuthClaimTypes.CLIENT_ID, serviceClient.ClientId);
        identity.SetClaim(AuthClaimTypes.SERVICE_NAME, serviceClient.ServiceName);

        foreach (string permission in serviceClient.ServicePermissions)
        {
            identity.AddClaim(new Claim(
                AuthClaimTypes.SERVICE_PERMISSION,
                permission));
        }

        identity.SetScopes(request.GetScopes());
        identity.SetResources(OidcScopes.GetResources(request.GetScopes()));
        identity.SetDestinations(GetDestinations);

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Помечает каждый service claim для включения только в access token.
    /// Client Credentials не создаёт ID token, потому что в flow нет пользователя.
    /// </summary>
    /// <param name="claim">
    /// Текущий claim, переданный OpenIddict. Сейчас все service claims имеют одно назначение.
    /// </param>
    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        yield return OpenIddictConstants.Destinations.AccessToken;
    }
}
