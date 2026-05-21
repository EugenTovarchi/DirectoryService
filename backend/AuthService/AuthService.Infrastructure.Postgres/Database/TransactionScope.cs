using System.Data;
using AuthService.Core.Abstractions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace AuthService.Infrastructure.Postgres.Database;

public sealed class TransactionScope : ITransactionScope
{
    private readonly IDbTransaction _transaction;
    private readonly ILogger<TransactionScope> _logger;

    public TransactionScope(IDbTransaction transaction, ILogger<TransactionScope> logger)
    {
        _transaction = transaction;
        _logger = logger;
    }

    public UnitResult<Error> Commit()
    {
        try
        {
            _transaction.Commit();
            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit AuthService transaction");
            return Errors.General.DatabaseError("commit.auth_service_transaction");
        }
    }

    public UnitResult<Error> Rollback()
    {
        try
        {
            _transaction.Rollback();
            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback AuthService transaction");
            return Errors.General.DatabaseError("rollback.auth_service_transaction");
        }
    }

    public void Dispose()
    {
        _transaction.Dispose();
    }
}
