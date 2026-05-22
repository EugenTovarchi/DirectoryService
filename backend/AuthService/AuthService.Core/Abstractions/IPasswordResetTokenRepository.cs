using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Core.Abstractions;

public interface IPasswordResetTokenRepository
{
    UnitResult<Error> Add(PasswordResetToken passwordResetToken);

    Task<PasswordResetToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task RevokeActiveTokensForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
