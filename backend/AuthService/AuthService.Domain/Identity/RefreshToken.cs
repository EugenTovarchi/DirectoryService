namespace AuthService.Domain.Identity;

/// <summary>
/// Серверная запись сессии. Raw refresh token не хранится, в БД попадает только hash.
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid userId,
        string tokenHash,
        DateTime expiresAt,
        string? createdByIp,
        string? userAgent)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        UserAgent = userAgent;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? CreatedByIp { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? UserAgent { get; private set; }
    public ApplicationUser User { get; private set; } = null!;
    public RefreshToken? ReplacedByToken { get; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;

    public void MarkUsed()
    {
        LastUsedAt = DateTime.UtcNow;
    }

    public void Revoke(string? revokedByIp, Guid? replacedByTokenId = null)
    {
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = revokedByIp;
        ReplacedByTokenId = replacedByTokenId;
    }
}
