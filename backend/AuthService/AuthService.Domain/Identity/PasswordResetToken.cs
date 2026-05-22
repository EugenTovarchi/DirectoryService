using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Domain.Identity;

public sealed class PasswordResetToken
{
    public const int TOKEN_HASH_LENGTH = 64;

    private PasswordResetToken()
    {
    }

    private PasswordResetToken(
        Guid userId,
        string tokenHash,
        DateTime expiresAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public ApplicationUser User { get; private set; } = null!;

    public bool IsActive => UsedAt is null && RevokedAt is null && DateTime.UtcNow < ExpiresAt;

    public static Result<PasswordResetToken, Error> Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAt)
    {
        if (userId == Guid.Empty)
            return Errors.General.EmptyId(userId);

        if (string.IsNullOrWhiteSpace(tokenHash))
            return Errors.General.ValueIsEmptyOrWhiteSpace("tokenHash");

        string normalizedTokenHash = tokenHash.Trim();
        if (normalizedTokenHash.Length != TOKEN_HASH_LENGTH)
            return Errors.General.ValueIsInvalid("tokenHash");

        if (expiresAt <= DateTime.UtcNow)
            return Errors.General.ValueIsInvalid("expiresAt");

        return new PasswordResetToken(
            userId,
            normalizedTokenHash,
            expiresAt);
    }

    public void MarkUsed()
    {
        UsedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        RevokedAt = DateTime.UtcNow;
    }
}
