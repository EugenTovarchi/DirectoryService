using CSharpFunctionalExtensions;
using FileService.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Quartz;

public class VideoProcessingScheduler : IVideoProcessingScheduler
{
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

            var jobKey = new JobKey($"VideoProcessing_{videoAssetId}", "VideoProcessingGroup");

            if (await scheduler.CheckExists(jobKey, cancellationToken))
            {
                _logger.LogWarning("Job for video {VideoAssetId} already exists, skipping", videoAssetId);
                return UnitResult.Success<Error>(); // Уже запланировано - ничего не делаем
            }

            var job = JobBuilder.Create<VideoProcessingJob>()
                .WithIdentity(jobKey)
                .UsingJobData("VideoAssetId", videoAssetId.ToString())

                // тут также нужно будет добавить счётик попыток с retry функционалом(приоритет, Id пользователя кто загрузил)
                .StoreDurably() // Задача сохраняется даже без триггеров
                .RequestRecovery() // Восстанавливать при перезапуске
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"Trigger_{videoAssetId}", "VideoProcessingGroup")
                .StartNow()
                .Build();

            _logger.LogInformation("Scheduling immediate processing for video {VideoAssetId}", videoAssetId);

            await scheduler.ScheduleJob(job, trigger, cancellationToken);

            _logger.LogInformation("Successfully scheduled video processing for {VideoAssetId}", videoAssetId);

            return UnitResult.Success<Error>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule video processing for {VideoAssetId}", videoAssetId);
            return Error.Failure("scheduler.unexpected.error", ex.Message);
        }
    }
}