using DirectoryService.Core.Abstractions;
using DirectoryService.Infrastructure.Postgres.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Infrastructure.Postgres.BackgroundServices;

public class DeleteExpireDepartmentsBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DeleteExpireDepartmentsBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting delete expired departments background service");

        using var timer =
            new PeriodicTimer(TimeSpan.FromHours(Constants.DELETE_EXPIRED_DEPARTMENT_SERVICE_INTERVAL_HOURS));

        try
        {
            await RunCleanUpAsync(cancellationToken);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RunCleanUpAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Background service stopping");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in delete expired departments service");
        }
        
        logger.LogInformation("Delete expired departments background service stopped");
    }

    private async Task RunCleanUpAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var deleteService = scope.ServiceProvider
                .GetRequiredService<DeleteExpiredDepartmentsService>();

            var result = await deleteService.Process(cancellationToken);
            if (result.IsFailure)
            {
                logger.LogError("Delete expired departments background service failed:" +
                                 "{Error}", result.Error);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in delete expired departments service");
        }
    }
}

