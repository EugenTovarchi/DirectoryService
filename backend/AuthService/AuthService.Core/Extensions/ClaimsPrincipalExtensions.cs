using System.Security.Claims;

namespace AuthService.Core.Extensions;

/// <summary>
/// Читает стандартные claims из проверенного OpenIddict principal.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    private const string SUBJECT_CLAIM = "sub";

    /// <summary>
    /// Возвращает идентификатор пользователя из стандартного subject claim.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        string? userId = user.FindFirstValue(SUBJECT_CLAIM);
        return Guid.TryParse(userId, out Guid parsedUserId)
            ? parsedUserId
            : Guid.Empty;
    }
}
