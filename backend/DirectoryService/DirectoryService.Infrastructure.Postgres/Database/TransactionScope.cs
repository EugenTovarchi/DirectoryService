using System.Data;
using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using DirectoryService.SharedKernel;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Infrastructure.Postgres.Database;

public class TransactionScope(IDbTransaction transaction, ILogger<TransactionScope> logger)
    : ITransactionScope
{
    public UnitResult<Error> Commit()
    {
        try
        {
            transaction.Commit();
            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to commit transaction");
            return Error.Failure("transaction.commit.failed", "Failed to commit transaction");
        }
    }

    public void Dispose()
    {
        transaction.Dispose();
    }

    public UnitResult<Error> Rollback()
    {
        try
        {
            transaction.Rollback();
            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rollback transaction");
            return Error.Failure("transaction.rollback.failed", "Failed to rollback transaction");
        }
    }
}
