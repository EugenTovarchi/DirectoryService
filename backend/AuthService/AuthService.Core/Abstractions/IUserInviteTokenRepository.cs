using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Core.Abstractions;

public interface IUserInviteTokenRepository
{
    UnitResult<Error> Add(UserInviteToken inviteToken);

    Task<UserInviteToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task RevokeActiveTokensForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
