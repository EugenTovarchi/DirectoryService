using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace AuthService.Web.Features.Oidc.Token;

/// <summary>
/// Тонкий HTTP adapter стандартного OAuth token endpoint.
/// Валидацию protocol parameters и client secret выполняет OpenIddict до вызова action.
/// </summary>
[Route("connect/token")]
public sealed class OidcTokenController : Controller
{
    private readonly OidcTokenHandler _handler;

    public OidcTokenController(OidcTokenHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Для Client Credentials создаёт service principal.
    /// Для authorization_code и refresh_token повторно использует principal,
    /// который OpenIddict сохранил внутри соответствующего protocol token.
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [SuppressMessage(
        "Security",
        "CA5391:Use antiforgery tokens in ASP.NET Core MVC controllers",
        Justification = "OAuth 2.0 token endpoint uses client authentication and is not a browser cookie form.")]
    public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict token request is missing");

        if (request.IsClientCredentialsGrantType())
        {
            ClaimsPrincipal? servicePrincipal = _handler.CreateServicePrincipal(request);
            if (servicePrincipal is null)
            {
                return Forbid(
                    CreateErrorProperties(
                        OpenIddictConstants.Errors.InvalidClient,
                        "The service client configuration is missing."),
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return SignIn(
                servicePrincipal,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType() ||
            request.IsRefreshTokenGrantType())
        {
            AuthenticateResult authentication = await HttpContext.AuthenticateAsync(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!authentication.Succeeded || authentication.Principal is null)
            {
                return Forbid(
                    CreateErrorProperties(
                        OpenIddictConstants.Errors.InvalidGrant,
                        "The authorization code or refresh token is invalid."),
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return SignIn(
                authentication.Principal,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Forbid(
            CreateErrorProperties(
                OpenIddictConstants.Errors.UnsupportedGrantType,
                "The specified grant type is not supported."),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static AuthenticationProperties CreateErrorProperties(
        string error,
        string description)
    {
        return new AuthenticationProperties(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            });
    }
}
