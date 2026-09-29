using System.Security.Claims;
using AuthService.Core.Services;
using AuthService.Domain.Identity;
using AuthService.Web.Authentication;

namespace AuthService.Web.Features.Oidc.BrowserLogin;

/// <summary>
/// Проверяет credentials и создаёт principal для короткой OIDC browser session.
/// Запись cookie остаётся в controller, потому что это HTTP/MVC responsibility.
/// </summary>
public sealed class OidcBrowserLoginHandler
{
    private readonly UserPasswordAuthenticator _passwordAuthenticator;

    public OidcBrowserLoginHandler(UserPasswordAuthenticator passwordAuthenticator)
    {
        _passwordAuthenticator = passwordAuthenticator;
    }

    /// <summary>
    /// Возвращает browser principal или null с одинаковой security-safe семантикой ошибки.
    /// </summary>
    public async Task<ClaimsPrincipal?> Handle(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await _passwordAuthenticator.AuthenticateAsync(
            email,
            password,
            cancellationToken);
        if (user is null)
            return null;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };
        var identity = new ClaimsIdentity(
            claims,
            OidcBrowserAuthenticationDefaults.SCHEME);

        return new ClaimsPrincipal(identity);
    }
}
