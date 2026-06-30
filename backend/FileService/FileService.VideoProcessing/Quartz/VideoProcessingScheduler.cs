using CSharpFunctionalExtensions;
using FileService.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Quartz;

public class VideoProcessingScheduler : IVideoProcessingScheduler
{
    public const string GROUP_NAME = "VideoProcessingGroup";

    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<VideoProcessingScheduler> _logger;

    public VideoProcessingScheduler(
        ISchedulerFactory schedulerFactory,
        ILogger<VideoProcessingScheduler> logger)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task<UnitResult<Error>> ScheduleProcessingAsync(
        Guid videoAssetId,
        string correlationId,
        DateTimeOffset? startAt = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            if (scheduler == null)
            {
                _logger.LogError("Scheduler is not available");
                return Error.Failure("scheduler.unavailable", "Scheduler is not available");
            }

            if (!scheduler.IsStarted)
            {
                _logger.LogWarning("Scheduler is not started, starting now...");
                await scheduler.Start(cancellationToken);
            }

            var jobKey = new JobKey($"VideoProcessing_{videoAssetId}", GROUP_NAME);
            DateTimeOffset scheduledAt = GetStartTime(startAt);

            if (await scheduler.CheckExists(jobKey, cancellationToken))
            {
                IReadOnlyCollection<ITrigger> existingTriggers =
                    await scheduler.GetTriggersOfJob(jobKey, cancellationToken);
                IReadOnlyCollection<IJobExecutionContext> executingJobs =
                    await scheduler.GetCurrentlyExecutingJobs(cancellationToken);
                bool isExecuting = executingJobs.Any(execution => execution.JobDetail.Key == jobKey);

                if (existingTriggers.Count > 0 || isExecuting)
                {
                    _logger.LogDebug(
                        "Video processing job for video asset {VideoAssetId} is already scheduled or running",
                        videoAssetId);
                    return UnitResult.Success<Error>();
                }

                var recoveryTrigger = TriggerBuilder.Create()
                    .WithIdentity($"Recovery_{videoAssetId}_{Guid.NewGuid():N}", GROUP_NAME)
                    .ForJob(jobKey)
                    .StartAt(scheduledAt)
                    .WithSimpleSchedule(schedule => schedule.WithMisfireHandlingInstructionFireNow())
                    .UsingJobData("CorrelationId", correlationId)
                    .Build();

                await scheduler.ScheduleJob(recoveryTrigger, cancellationToken);
                _logger.LogInformation(
                    "Restored trigger for video asset {VideoAssetId} at {ScheduledAt}",
                    videoAssetId,
                    scheduledAt);

                return UnitResult.Success<Error>();
            }

            var job = JobBuilder.Create<VideoProcessingJob>()
                .WithIdentity(jobKey)
                .UsingJobData("VideoAssetId", videoAssetId.ToString())
                .UsingJobData("AttemptNumber", "1")
                .UsingJobData("CorrelationId", correlationId)
                .StoreDurably() // Задача сохраняется даже без триггеров
                .RequestRecovery() // Восстанавливать при перезапуске
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"Trigger_{videoAssetId}", GROUP_NAME)
                .StartAt(scheduledAt)
                .WithSimpleSchedule(schedule => schedule.WithMisfireHandlingInstructionFireNow())
                .Build();

            await scheduler.ScheduleJob(job, trigger, cancellationToken);

            _logger.LogInformation(
                "Scheduled video processing for video asset {VideoAssetId} at {ScheduledAt}",
                videoAssetId,
                scheduledAt);

            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule video processing for {VideoAssetId}", videoAssetId);
            return Error.Failure("scheduler.unexpected.error", ex.Message);
        }
    }

    private static DateTimeOffset GetStartTime(DateTimeOffset? startAt)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return startAt.HasValue && startAt.Value > now ? startAt.Value : now;
    }
}
