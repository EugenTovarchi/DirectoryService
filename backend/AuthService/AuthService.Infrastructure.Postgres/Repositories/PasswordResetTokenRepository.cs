using AuthService.Core.Abstractions;
using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using SharedService.SharedKernel;

namespace AuthService.Infrastructure.Postgres.Repositories;

public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly AuthServiceDbContext _dbContext;

    public PasswordResetTokenRepository(AuthServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public UnitResult<Error> Add(PasswordResetToken passwordResetToken)
    {
        if (passwordResetToken is null)
            return Errors.General.ValueIsInvalid("passwordResetToken");

        _dbContext.PasswordResetTokens.Add(passwordResetToken);

        return UnitResult.Success<Error>();
    }

    public Task<PasswordResetToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.PasswordResetTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async Task RevokeActiveTokensForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        List<PasswordResetToken> activeTokens = await _dbContext.PasswordResetTokens
            .Where(token => token.UserId == userId
                            && token.UsedAt == null
                            && token.RevokedAt == null
                            && token.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (PasswordResetToken token in activeTokens)
        {
            token.Revoke();
        }
    }
}
