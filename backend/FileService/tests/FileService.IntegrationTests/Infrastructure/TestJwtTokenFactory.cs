using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FileService.IntegrationTests.Infrastructure;

public static class TestJwtTokenFactory
{
    public static string Create(string? permission = null) =>
        CreateToken(permission, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5));

    public static string CreateExpired(string? permission = null) =>
        CreateToken(permission, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(-1));

    private static string CreateToken(string? permission, DateTime notBefore, DateTime expires)
    {
        List<Claim> claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())
        ];

        if (permission != null)
        {
            claims.Add(new Claim("permission", permission));
        }

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(FileServiceTestWebFactory.TEST_JWT_SIGNING_KEY)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            FileServiceTestWebFactory.TEST_JWT_ISSUER,
            FileServiceTestWebFactory.TEST_JWT_AUDIENCE,
            claims,
            notBefore,
            expires,
            signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
