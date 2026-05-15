using AuthService.Domain.Identity;

namespace AuthService.Core.Abstractions;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(
        ApplicationUser user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions);

    RefreshTokenResult CreateRefreshToken();

    string HashRefreshToken(string rawRefreshToken);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);

public sealed record RefreshTokenResult(string RawToken, string TokenHash);
