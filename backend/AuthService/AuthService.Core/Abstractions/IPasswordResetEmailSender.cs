using AuthService.Core.Models;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Core.Abstractions;

public interface IPasswordResetEmailSender
{
    Task<UnitResult<Error>> SendPasswordResetAsync(
        PasswordResetEmailMessage message,
        CancellationToken cancellationToken = default);
}
