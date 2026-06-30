using FileService.Core.Abstractions;
using FileService.VideoProcessing.Pipeline.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileService.VideoProcessing.Quartz;

public sealed class VideoProcessingRecoveryService(
    IServiceScopeFactory scopeFactory,
    IOptions<VideoProcessingOptions> options,
    ILogger<VideoProcessingRecoveryService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RecoverAsync(stoppingToken);

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(options.Value.RecoveryScanIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IVideoProcessesRepository>();
            var scheduler = scope.ServiceProvider.GetRequiredService<IVideoProcessingScheduler>();

            var recoverableResult = await repository.GetRecoverableVideoProcessesAsync(cancellationToken);
            if (recoverableResult.IsFailure)
            {
                logger.LogError(
                    "Video processing recovery scan failed. Error code: {ErrorCode}",
                    recoverableResult.Error.Code);
                return;
            }

            int scheduledCount = 0;
            foreach (RecoverableVideoProcess process in recoverableResult.Value)
            {
                var scheduleResult = await scheduler.ScheduleProcessingAsync(
                    process.VideoAssetId,
                    process.CorrelationId,
                    process.NextRetryAt.HasValue
                        ? new DateTimeOffset(DateTime.SpecifyKind(process.NextRetryAt.Value, DateTimeKind.Utc))
                        : null,
                    cancellationToken: cancellationToken);
                if (scheduleResult.IsFailure)
                {
                    logger.LogError(
                        "Failed to recover video processing job for video asset {VideoAssetId}. Error code: {ErrorCode}",
                        process.VideoAssetId,
                        scheduleResult.Error.Code);
                    continue;
                }

                scheduledCount++;
            }

            if (recoverableResult.Value.Count > 0)
            {
                logger.LogDebug(
                    "Video processing recovery scan completed. Candidates: {CandidateCount}, scheduled or present: {ScheduledCount}",
                    recoverableResult.Value.Count,
                    scheduledCount);
            }
            else
            {
                logger.LogDebug("Video processing recovery scan found no candidates");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Video processing recovery scan was cancelled during shutdown");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected video processing recovery failure");
        }
    }
}
