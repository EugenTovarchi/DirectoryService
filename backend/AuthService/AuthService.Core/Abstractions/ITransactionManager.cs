using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Core.Abstractions;

public interface ITransactionManager
{
    Task<UnitResult<Error>> SaveChangeAsync(CancellationToken cancellationToken = default);
}
