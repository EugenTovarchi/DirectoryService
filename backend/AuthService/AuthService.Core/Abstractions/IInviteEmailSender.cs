using AuthService.Core.Models;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Core.Abstractions;

public interface IInviteEmailSender
{
    Task<UnitResult<Error>> SendInviteAsync(
        InviteEmailMessage message,
        CancellationToken cancellationToken = default);
}
