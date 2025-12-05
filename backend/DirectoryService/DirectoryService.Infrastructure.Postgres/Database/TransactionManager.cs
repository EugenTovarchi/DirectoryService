using CSharpFunctionalExtensions;
using DirectoryService.Application.Database;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;

namespace DirectoryService.Infrastructure.Postgres.Database;

public class TransactionManager : ITrasactionManager
{
    private readonly DirectoryServiceDbContext _dbContext;
    private readonly ILogger<TransactionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public TransactionManager(
        DirectoryServiceDbContext dbContext,    
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
            var transaction = await _dbContext.Database.BeginTransactionAsync(level ?? IsolationLevel.ReadCommitted, cancellationToken);
            var transactionScopeLogger = _loggerFactory.CreateLogger<TransactionScope>();
            var transactionScope = new TransactionScope(transaction.GetDbTransaction(), transactionScopeLogger);

            return transactionScope;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to begin transaction");
            return Error.Failure("database", "Failed to begin transaction");
        }
    }
    public async Task<UnitResult<Error>> SaveChangeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var entries = _dbContext.ChangeTracker.Entries();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to save changes");
            return Error.Failure("database", "Failed to save changes");
        }
    }
}
