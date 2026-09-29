using AuthService.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace AuthService.Core.Services;

/// <summary>
/// Единообразно проверяет email/password для API login и будущей OIDC browser session.
/// Наружу намеренно возвращается только user или null: неизвестный, inactive, locked user
/// и неверный password не должны различаться по форме ошибки.
/// </summary>
public sealed class UserPasswordAuthenticator
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public UserPasswordAuthenticator(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    /// <summary>
    /// Проверяет account state и password с включённым Identity lockout counter.
    /// Метод не создаёт cookie или token — это ответственность вызывающего flow.
    /// </summary>
    public async Task<ApplicationUser?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ApplicationUser? user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive)
            return null;

        // Старые users могли появиться до настройки lockout.
        // Включаем его лениво, сохраняя одинаковый public failure.
        if (!user.LockoutEnabled)
        {
            IdentityResult lockoutResult = await _userManager.SetLockoutEnabledAsync(
                user,
                enabled: true);
            if (!lockoutResult.Succeeded)
                return null;
        }

        // lockoutOnFailure увеличивает AccessFailedCount и временно блокирует
        // дальнейшие попытки после настроенного количества ошибок.
        SignInResult passwordResult = await _signInManager.CheckPasswordSignInAsync(
            user,
            password,
            lockoutOnFailure: true);

        cancellationToken.ThrowIfCancellationRequested();

        return passwordResult.Succeeded ? user : null;
    }
}
