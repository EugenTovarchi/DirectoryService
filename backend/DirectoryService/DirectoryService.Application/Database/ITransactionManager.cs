using System.Data;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace DirectoryService.Application.Database;

public interface ITransactionManager
{
    Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
        CancellationToken cancellationToken = default,
        IsolationLevel? level = null);
    Task<UnitResult<Error>> SaveChangeAsync(CancellationToken cancellationToken);
}