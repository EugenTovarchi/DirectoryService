using System.Security.Claims;
using AuthService.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Web.Features.Oidc.BrowserLogin;

/// <summary>
/// Создаёт короткую browser session перед интерактивным OIDC authorization request.
/// Controller не выпускает access/refresh tokens и не используется resource API.
/// </summary>
[Route("connect/login")]
public sealed class OidcLoginController : Controller
{
    private const string VIEW_PATH = "~/Features/Oidc/BrowserLogin/Login.cshtml";
    private readonly OidcBrowserLoginHandler _handler;

    public OidcLoginController(OidcBrowserLoginHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Показывает login form и сохраняет локальный адрес возврата к authorization request.
    /// </summary>
    [HttpGet]
    public IActionResult Login([FromQuery] string returnUrl)
    {
        if (!Url.IsLocalUrl(returnUrl))
            return BadRequest();

        return View(VIEW_PATH, new OidcLoginModel { ReturnUrl = returnUrl });
    }

    /// <summary>
    /// Проверяет credentials общим Identity authenticator и создаёт защищённую cookie.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        OidcLoginModel model,
        CancellationToken cancellationToken)
    {
        if (!Url.IsLocalUrl(model.ReturnUrl))
            return BadRequest();

        if (!ModelState.IsValid)
            return View(VIEW_PATH, model);

        ClaimsPrincipal? principal = await _handler.Handle(
            model.Email,
            model.Password,
            cancellationToken);
        if (principal is null)
        {
            // Одинаковый текст не раскрывает, существует ли account и почему login отклонён.
            model.ErrorMessage = "Не удалось выполнить вход.";
            return View(VIEW_PATH, model);
        }

        await HttpContext.SignInAsync(
            OidcBrowserAuthenticationDefaults.SCHEME,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false
            });

        return LocalRedirect(model.ReturnUrl);
    }
}
