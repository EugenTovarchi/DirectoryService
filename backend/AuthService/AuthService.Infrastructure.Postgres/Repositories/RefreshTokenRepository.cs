using AuthService.Core.Abstractions;
using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
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
}
