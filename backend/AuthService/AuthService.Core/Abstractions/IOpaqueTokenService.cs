namespace AuthService.Core.Abstractions;

/// <summary>
/// Создаёт непрозрачные одноразовые tokens и hashes для безопасного хранения.
/// Не выпускает OAuth/OIDC или JWT tokens.
/// </summary>
public interface IOpaqueTokenService
{
    /// <summary>
    /// Создаёт криптографически стойкий raw token и его SHA-256 hash.
    /// Raw token передаётся пользователю, а в БД сохраняется только hash.
    /// </summary>
    OpaqueToken CreateToken();

    /// <summary>
    /// Вычисляет SHA-256 hash полученного raw token для поиска сохранённой записи.
    /// </summary>
    string HashToken(string rawToken);
}

/// <summary>
/// Содержит raw token для доставки и hash для безопасного хранения.
/// </summary>
public sealed record OpaqueToken(string RawToken, string TokenHash);
