using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AuthService.Core.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        string? userId = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(userId, out Guid parsedUserId)
            ? parsedUserId
            : Guid.Empty;
    }
}
