using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Server.AspNetCore;

namespace AuthService.Web.Features.Oidc.UserInfo;

/// <summary>
/// Тонкий HTTP adapter для OpenID Connect UserInfo endpoint.
/// </summary>
[Route("connect/userinfo")]
public sealed class OidcUserInfoController : Controller
{
    private readonly OidcUserInfoHandler _handler;

    public OidcUserInfoController(OidcUserInfoHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Возвращает UserInfo только после protocol validation Bearer access token.
    /// Состав response дополнительно ограничивается scopes, выданными этому token.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> UserInfo(CancellationToken cancellationToken)
    {
        AuthenticateResult authentication = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!authentication.Succeeded)
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        OidcUserInfoResponse? response = await _handler.Handle(
            authentication.Principal!,
            cancellationToken);
        if (response is null)
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        return Ok(response);
    }
}
