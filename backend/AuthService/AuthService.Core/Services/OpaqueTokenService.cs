using System.Security.Cryptography;
using System.Text;
using AuthService.Core.Abstractions;

namespace AuthService.Core.Services;

/// <summary>
/// Создаёт URL-safe opaque tokens из криптографически стойких случайных bytes.
/// Внешнему получателю передаётся raw token, а persistence использует только SHA-256 hash.
/// </summary>
public sealed class OpaqueTokenService : IOpaqueTokenService
{
    private const int TOKEN_BYTES = 64;

    /// <summary>
    /// Создаёт новый opaque token с 512 bits случайной энтропии.
    /// Hex encoding не требует escaping при передаче token в URL.
    /// </summary>
    public OpaqueToken CreateToken()
    {
        string rawToken = Convert
            .ToHexString(RandomNumberGenerator.GetBytes(TOKEN_BYTES))
            .ToLowerInvariant();

        return new OpaqueToken(rawToken, HashToken(rawToken));
    }

    /// <summary>
    /// Возвращает lowercase SHA-256 hash для стабильного сравнения с записью в БД.
    /// </summary>
    public string HashToken(string rawToken)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
