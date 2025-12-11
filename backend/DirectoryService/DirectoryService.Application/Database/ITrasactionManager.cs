using CSharpFunctionalExtensions;
using DirectoryService.SharedKernel;
using System.Data;

namespace DirectoryService.Application.Database;

public interface ITrasactionManager
{
    Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
        CancellationToken cancellationToken = default,
        IsolationLevel? level = null);
    Task<UnitResult<Error>> SaveChangeAsync(CancellationToken cancellationToken);
}