using AuthService.Core.Abstractions;
using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using SharedService.SharedKernel;

namespace AuthService.Infrastructure.Postgres.Repositories;

public sealed class UserInviteTokenRepository : IUserInviteTokenRepository
{
    private readonly AuthServiceDbContext _dbContext;

    public UserInviteTokenRepository(AuthServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public UnitResult<Error> Add(UserInviteToken inviteToken)
    {
        if (inviteToken is null)
            return Errors.General.ValueIsInvalid("inviteToken");

        _dbContext.UserInviteTokens.Add(inviteToken);

        return UnitResult.Success<Error>();
    }

    public Task<UserInviteToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.UserInviteTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async Task RevokeActiveTokensForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        List<UserInviteToken> activeTokens = await _dbContext.UserInviteTokens
            .Where(token => token.UserId == userId
                            && token.AcceptedAt == null
                            && token.RevokedAt == null
                            && token.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (UserInviteToken token in activeTokens)
        {
            token.Revoke();
        }
    }
}
