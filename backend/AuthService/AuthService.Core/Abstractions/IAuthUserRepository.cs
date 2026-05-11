using System.Linq.Expressions;
using AuthService.Domain.Users;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Core.Abstractions;

public interface IAuthUserRepository
{
    Result<Guid, Error> Add(AuthUser user);

    Task<Result<AuthUser, Error>> GetBy(
        Expression<Func<AuthUser, bool>> predicate,
        CancellationToken cancellationToken = default);
}
