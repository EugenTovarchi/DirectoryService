using CSharpFunctionalExtensions;
using DirectoryService.Core.Abstractions;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.Infrastructure.Postgres.Repositories;
using DirectoryService.SharedKernel;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Infrastructure.Postgres.Services;

public class DeleteExpiredDepartmentsService(
    DepartmentRepository departmentRepository,
    DirectoryServiceDbContext dbContext,
    ILogger<DeleteExpiredDepartmentsService> logger)
{
    public async Task<UnitResult<Error>> Process(CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

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

            var descendantIds = await departmentRepository.GetDescendantDepartmentIds(
                expiredDepartments, cancellationToken);
            if (descendantIds.Count != 0)
            {
                logger.LogInformation("Found {Count} descendants to update", descendantIds.Count);

                var lockResult = await departmentRepository.LockDescendantsByIds(
                    descendantIds, cancellationToken);
                if (lockResult.IsFailure)
                {
                    logger.LogWarning("Failed to lock descendants: {Error}", lockResult.Error);

                    await transaction.RollbackAsync(cancellationToken);
                    return lockResult.Error;
                }

                var updateDescendantsInfo = await departmentRepository
                    .UpdateDescendantsInfoAfterCleanUp(descendantIds, cancellationToken);
                if (updateDescendantsInfo.IsFailure)
                {
                    logger.LogError("Failed to update descendants: {Error}", updateDescendantsInfo.Error);

                    await transaction.RollbackAsync(cancellationToken);
                    return updateDescendantsInfo.Error;
                }

                logger.LogInformation("Updated {Count} descendants", descendantIds.Count);
            }

            int deleteExpiredDepartments = await departmentRepository.CleanExpiredDepartmentsWithRelatesAsync(
                expiredDepartments, cancellationToken);
            logger.LogInformation("Successfully deleted {Count} departments", deleteExpiredDepartments);

            await transaction.CommitAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogCritical(e, "Critical error during  delete expired departments");
            return Errors.General.DatabaseError("delete.expired_departments");
        }
    }
}