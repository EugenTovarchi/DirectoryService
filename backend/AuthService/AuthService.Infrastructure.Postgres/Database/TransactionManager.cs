using AuthService.Core.Abstractions;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace AuthService.Infrastructure.Postgres.Database;

public class TransactionManager(
    AuthServiceDbContext dbContext,
    ILogger<TransactionManager> logger)
    : ITransactionManager
{
    public async Task<UnitResult<Error>> SaveChangeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Database error while saving AuthService changes");
            return Errors.General.DatabaseError("save.auth_service_changes");
        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(ex, "Operation was cancelled while saving AuthService changes");
            return Errors.General.DatabaseError("save.auth_service_changes");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while saving AuthService changes");
            return Errors.General.DatabaseError("save.auth_service_changes");
        }
    }
}
