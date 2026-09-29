using System.Security.Claims;
using AuthService.Web.Authentication;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace AuthService.Web.Features.Oidc.Authorization;

/// <summary>
/// HTTP adapter для authorization endpoint: browser authentication, consent и protocol result.
/// </summary>
[Route("connect/authorize")]
public sealed class OidcAuthorizationController : Controller
{
    private const string VIEW_PATH = "~/Features/Oidc/Authorization/Consent.cshtml";
    private readonly OidcAuthorizationHandler _handler;

    public OidcAuthorizationController(OidcAuthorizationHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Проверяет короткую browser session и показывает consent page для валидного OIDC request.
    /// Если browser session ещё нет, authentication scheme перенаправит пользователя на login form.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict authorization request is missing");

        AuthenticateResult authentication = await HttpContext.AuthenticateAsync(
            OidcBrowserAuthenticationDefaults.SCHEME);
        if (!authentication.Succeeded)
        {
            string returnUrl = Request.PathBase + Request.Path + Request.QueryString;
            return Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                OidcBrowserAuthenticationDefaults.SCHEME);
        }

        OidcAuthorizationModel? model = await _handler.GetConsentAsync(
            request,
            authentication.Principal!,
            cancellationToken);
        if (model is null)
            return Forbid(OidcBrowserAuthenticationDefaults.SCHEME);

        return View(VIEW_PATH, model);
    }

    /// <summary>
    /// Принимает решение с consent page и возвращает OpenIddict protocol result.
    /// При согласии OpenIddict создаст authorization code; при отказе вернёт стандартный access_denied.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Authorize(
        [FromForm] string decision,
        CancellationToken cancellationToken)
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict authorization request is missing");

        if (!string.Equals(decision, "accept", StringComparison.Ordinal))
        {
            return Forbid(
                new AuthenticationProperties(
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                            OpenIddictConstants.Errors.AccessDenied,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The authorization request was denied."
                    }),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        AuthenticateResult authentication = await HttpContext.AuthenticateAsync(
            OidcBrowserAuthenticationDefaults.SCHEME);
        if (!authentication.Succeeded)
            return Challenge(OidcBrowserAuthenticationDefaults.SCHEME);

        ClaimsPrincipal? principal = await _handler.CreateTokenPrincipalAsync(
            request,
            authentication.Principal!,
            cancellationToken);
        if (principal is null)
            return Challenge(OidcBrowserAuthenticationDefaults.SCHEME);

        return SignIn(
            principal,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
