using AuthService.Core.Abstractions;
using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using SharedService.SharedKernel;

namespace AuthService.Infrastructure.Postgres.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AuthServiceDbContext _dbContext;

    public RefreshTokenRepository(AuthServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public UnitResult<Error> Add(RefreshToken refreshToken)
    {
        if (refreshToken is null)
            return Errors.General.ValueIsInvalid("refreshToken");

        _dbContext.RefreshTokens.Add(refreshToken);

        return UnitResult.Success<Error>();
    }

    public Task<RefreshToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveSessionsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RefreshTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId
                            && token.RevokedAt == null
                            && token.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(token => token.LastUsedAt ?? token.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RevokeActiveTokensForUserAsync(
        Guid userId,
        string? revokedByIp,
        CancellationToken cancellationToken = default)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId
                            && token.RevokedAt == null
                            && token.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(revokedByIp);
        }
    }
}
