using System.Data;
using CSharpFunctionalExtensions;
using DirectoryService.SharedKernel;

namespace DirectoryService.Application.Database;

public interface ITransactionManager
{
    Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
        IsolationLevel? level = null,
        CancellationToken cancellationToken = default);
    Task<UnitResult<Error>> SaveChangeAsync(CancellationToken cancellationToken);
}