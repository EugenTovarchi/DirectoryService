using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthService.Core.Abstractions;
using AuthService.Core.Options;
using AuthService.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Core.Services;

/// <summary>
/// Выпускает access JWT и refresh token. Raw refresh token возвращается клиенту, в БД сохраняется только hash.
/// </summary>
public sealed class TokenService : ITokenService
{
    private const int REFRESH_TOKEN_BYTES = 64;

    private readonly JwtOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public TokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public AccessTokenResult CreateAccessToken(
        ApplicationUser user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions)
    {
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenLifetimeMinutes);
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.DisplayName is not null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, user.DisplayName.Value));

        if (user.CurrentCompanyId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.COMPANY_ID, user.CurrentCompanyId.Value.ToString()));

        claims.AddRange(roles.Select(role => new Claim(AuthClaimTypes.ROLE, role)));
        claims.AddRange(permissions.Select(permission => new Claim(AuthClaimTypes.PERMISSION, permission)));

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        return new AccessTokenResult(_tokenHandler.WriteToken(token), expiresAt);
    }

    public RefreshTokenResult CreateRefreshToken()
    {
        string rawToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(REFRESH_TOKEN_BYTES));
        return new RefreshTokenResult(rawToken, HashRefreshToken(rawToken));
    }

    public string HashRefreshToken(string rawRefreshToken)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
