using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Domain.Identity;

/// <summary>
/// Одноразовый invite token для первичной установки пароля приглашенным пользователем.
/// </summary>
public sealed class UserInviteToken
{
    public const int TOKEN_HASH_LENGTH = 64;

    private UserInviteToken()
    {
    }

    private UserInviteToken(
        Guid userId,
        Guid createdByUserId,
        string tokenHash,
        DateTime expiresAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        CreatedByUserId = createdByUserId;
        TokenHash = tokenHash;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public ApplicationUser User { get; private set; } = null!;
    public ApplicationUser CreatedByUser { get; private set; } = null!;

    public bool IsActive => AcceptedAt is null && RevokedAt is null && DateTime.UtcNow < ExpiresAt;

    public static Result<UserInviteToken, Error> Create(
        Guid userId,
        Guid createdByUserId,
        string tokenHash,
        DateTime expiresAt)
    {
        if (userId == Guid.Empty)
            return Errors.General.EmptyId(userId);

        if (createdByUserId == Guid.Empty)
            return Errors.General.EmptyId(createdByUserId);

        if (string.IsNullOrWhiteSpace(tokenHash))
            return Errors.General.ValueIsEmptyOrWhiteSpace("tokenHash");

        string normalizedTokenHash = tokenHash.Trim();
        if (normalizedTokenHash.Length != TOKEN_HASH_LENGTH)
            return Errors.General.ValueIsInvalid("tokenHash");

        if (expiresAt <= DateTime.UtcNow)
            return Errors.General.ValueIsInvalid("expiresAt");

        return new UserInviteToken(
            userId,
            createdByUserId,
            normalizedTokenHash,
            expiresAt);
    }

    public void Accept()
    {
        AcceptedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        RevokedAt = DateTime.UtcNow;
    }
}
