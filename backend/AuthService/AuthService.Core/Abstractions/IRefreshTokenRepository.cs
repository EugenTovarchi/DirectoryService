using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Core.Abstractions;

public interface IRefreshTokenRepository
{
    UnitResult<Error> Add(RefreshToken refreshToken);

    Task<RefreshToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task RevokeActiveTokensForUserAsync(
        Guid userId,
        string? revokedByIp,
        CancellationToken cancellationToken = default);
}
