using CSharpFunctionalExtensions;
using DirectoryService.SharedKernel;

namespace DirectoryService.Application.Database;

public interface ITrasactionManager
{
    Task<Result<ITransactionScope, Error>> BeginTransactionAsync(CancellationToken cancellationToken);
    Task<UnitResult<Error>> SaveChangeAsync(CancellationToken cancellationToken);
}