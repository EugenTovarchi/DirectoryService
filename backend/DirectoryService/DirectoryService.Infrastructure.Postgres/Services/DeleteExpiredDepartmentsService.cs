using CSharpFunctionalExtensions;
using DirectoryService.Core.Abstractions;
using DirectoryService.Infrastructure.Postgres.Database;
using DirectoryService.Infrastructure.Postgres.Repositories;
using DirectoryService.SharedKernel;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Infrastructure.Postgres.Services;

public class DeleteExpiredDepartmentsService(
    DepartmentRepository departmentRepository,
    TransactionManager transactionManager,
    ILogger<DeleteExpiredDepartmentsService> logger)
{
    public async Task<UnitResult<Error>> Process(CancellationToken cancellationToken)
    {
        var transaction = await transactionManager.BeginTransactionAsync(cancellationToken);
        using var transactionScope = transaction.Value;
        
        try
        {
            var expiredDepartments = await departmentRepository.GetExpiredDepartmentsIds(
                Constants.DAYS_UNTIL_PERMANENT_DELETION, cancellationToken);
            if (expiredDepartments.Count == 0)
            {
                logger.LogDebug("No expired departments found");
                return UnitResult.Success<Error>();
            }
            
            logger.LogInformation("Found {Count} expired departments to delete", expiredDepartments.Count);
            
            var descendantIds  = await departmentRepository.GetDescendantDepartmentIds(
                expiredDepartments, cancellationToken);
            if (descendantIds.Count != 0)
            {
                logger.LogInformation("Found {Count} descendants to update", descendantIds.Count);
                
                var lockResult = await departmentRepository.LockDescendantsByIds(
                    descendantIds , cancellationToken);
                if (lockResult.IsFailure)
                {
                    logger.LogWarning("Failed to lock descendants: {Error}", lockResult.Error);
                    
                    transactionScope.Rollback();
                    return lockResult.Error;
                }
            
                var updateDescendantsInfo = await departmentRepository
                    .UpdateDescendantsInfoAfterCleanUp(expiredDepartments, descendantIds , cancellationToken);
                if (updateDescendantsInfo.IsFailure)
                {
                    logger.LogError("Failed to update descendants: {Error}", updateDescendantsInfo.Error);
                    return updateDescendantsInfo.Error;
                }
                
                logger.LogInformation("Updated {Count} descendants", descendantIds.Count);
            }
            
            var deleteExpiredDepartments = await departmentRepository.CleanExpiredDepartmentsWithRelatesAsync(
                expiredDepartments, cancellationToken);
            logger.LogInformation("Successfully deleted {Count} departments", deleteExpiredDepartments);
            
            transactionScope.Commit();
            return UnitResult.Success<Error>();
        }
        catch (Exception e)
        {
            transactionScope.Rollback();
            logger.LogCritical(e, "Critical error during  delete expired departments");
            return Errors.General.DatabaseError("delete.expired_departments");
        }
    }
}