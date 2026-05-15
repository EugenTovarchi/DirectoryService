using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Core.Abstractions;

public interface IRefreshTokenRepository
{
    UnitResult<Error> Add(RefreshToken refreshToken);
}
