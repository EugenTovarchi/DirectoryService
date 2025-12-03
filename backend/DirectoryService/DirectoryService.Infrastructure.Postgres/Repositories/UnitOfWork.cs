using DirectoryService.Core.Database;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace DirectoryService.Infrastructure.Postgres.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly DirectoryServiceDbContext _dbContext;

    public UnitOfWork(DirectoryServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        return transaction.GetDbTransaction();
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

