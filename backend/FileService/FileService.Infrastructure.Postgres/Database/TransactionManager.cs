using System.Data;
using CSharpFunctionalExtensions;
using FileService.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.Infrastructure.Postgres.Database;

public class TransactionManager : ITransactionManager
{
    private readonly ILogger<TransactionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly FileServiceDbContext _dbContext;
    private IDbContextTransaction? _transaction;

    public TransactionManager(
        ILogger<TransactionManager> logger,
        ILoggerFactory loggerFactory,
        FileServiceDbContext dbContext)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _dbContext = dbContext;
    }

    public async Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
        CancellationToken cancellationToken = default,
        IsolationLevel? level = null)
    {
        try
        {
            _transaction = await _dbContext.Database.BeginTransactionAsync(level ?? IsolationLevel.ReadCommitted,
                cancellationToken);
            var transactionScopeLogger = _loggerFactory.CreateLogger<TransactionScope>();
            var transactionScope = new TransactionScope(_transaction.GetDbTransaction(), transactionScopeLogger);

            return transactionScope;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to begin transaction");
            return Error.Failure("database", "Failed to begin transaction");
        }
    }

    public async Task<UnitResult<Error>> CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            return Errors.General.ValueIsRequired("transaction");
        try
        {
            await _transaction.CommitAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                "Database concurrency conflict while committing transaction. Conflicting entries: {ConflictingEntryCount}",
                ex.Entries.Count);
            await RollbackAsync(cancellationToken);
            return Error.Failure("database", "Concurrency conflict");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                "Database update failed while committing transaction. Exception type: {ExceptionType}",
                ex.GetType().Name);
            await RollbackAsync(cancellationToken);
            return Error.Failure("database", "Database update failed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Database transaction commit was canceled");
            await RollbackAsync(cancellationToken);
            return Error.Failure("database", "Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit transaction");
            await RollbackAsync(cancellationToken);
            return Error.Failure("database", "Failed to commit transaction");
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    private async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if(_transaction is not null)
                await _transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to rollback transaction");
        }
    }

    public async Task<UnitResult<Error>> SaveChangeAsync(CancellationToken cancellationToken)
    {
        try
        {
           await _dbContext.SaveChangesAsync(cancellationToken);

           return UnitResult.Success<Error>();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                "Database concurrency conflict while saving changes. Conflicting entries: {ConflictingEntryCount}",
                ex.Entries.Count);
            return Error.Failure("database", "Concurrency conflict");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                "Database update failed while saving changes. Exception type: {ExceptionType}",
                ex.GetType().Name);
            return Error.Failure("database", "Database update failed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Saving database changes was canceled");
            return Error.Failure("database", "Operation cancelled");
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to save changes");
            return Error.Failure("database", "Failed to save changes");
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
