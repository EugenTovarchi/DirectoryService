using System.Data;
using AuthService.Core.Abstractions;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace AuthService.Infrastructure.Postgres.Database;

public class TransactionManager : ITransactionManager
{
    private readonly AuthServiceDbContext _dbContext;
    private readonly ILogger<TransactionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public TransactionManager(
        AuthServiceDbContext dbContext,
        ILogger<TransactionManager> logger,
        ILoggerFactory loggerFactory)
    {
        _dbContext = dbContext;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
        CancellationToken cancellationToken = default,
        IsolationLevel? level = null)
    {
        try
        {
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    level ?? IsolationLevel.ReadCommitted,
                    cancellationToken);

            ILogger<TransactionScope> transactionScopeLogger =
                _loggerFactory.CreateLogger<TransactionScope>();

            return new TransactionScope(transaction.GetDbTransaction(), transactionScopeLogger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to begin AuthService transaction");
            return Errors.General.DatabaseError("begin.auth_service_transaction");
        }
    }

    public async Task<UnitResult<Error>> SaveChangeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while saving AuthService changes");
            return Errors.General.DatabaseError("save.auth_service_changes");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Operation was cancelled while saving AuthService changes");
            return Errors.General.DatabaseError("save.auth_service_changes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while saving AuthService changes");
            return Errors.General.DatabaseError("save.auth_service_changes");
        }
    }
}
