    using System.Diagnostics;
    using System.Globalization;
    using FileService.Core.Abstractions;
    using FileService.Domain;
    using FileService.Domain.MediaProcessing;
    using FileService.VideoProcessing.Pipeline;
    using Microsoft.Extensions.Logging;
    using Quartz;
    using SharedService.SharedKernel;

    namespace FileService.VideoProcessing.Quartz;

    [DisallowConcurrentExecution]
    public class VideoProcessingJob : IJob
    {
        private readonly IVideoProcessingService _videoProcessingService;
        private readonly IVideoProcessesRepository _videoProcessesRepository;
        private readonly IMediaAssetsRepository _mediaAssetsRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly ILogger<VideoProcessingJob> _logger;
        private readonly IVideoProcessingPolicy _processingPolicy;
        private readonly VideoProcessingTelemetry _telemetry;

        public VideoProcessingJob(
            IVideoProcessingService videoProcessingService,
            IMediaAssetsRepository mediaAssetsRepository,
            ILogger<VideoProcessingJob> logger,
            ITransactionManager transactionManager,
            IVideoProcessesRepository videoProcessesRepository,
            IVideoProcessingPolicy processingPolicy,
            VideoProcessingTelemetry telemetry)
        {
            _videoProcessingService = videoProcessingService;
            _mediaAssetsRepository = mediaAssetsRepository;
            _logger = logger;
            _transactionManager = transactionManager;
            _videoProcessesRepository = videoProcessesRepository;
            _processingPolicy = processingPolicy;
            _telemetry = telemetry;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var cancellationToken = context.CancellationToken;
            var jobDataMap = context.MergedJobDataMap;

            if (!TryGetGuidFromJobData(jobDataMap, "VideoAssetId", out var videoAssetId))
            {
                _logger.LogError(
                    "Invalid or missing VideoAssetId in job data for Quartz job {JobKey}",
                    context.JobDetail.Key);
                await DeleteJobAsync(context);
                return;
            }

            string? correlationId = jobDataMap["CorrelationId"]?.ToString();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
                _logger.LogWarning(
                    "Missing CorrelationId in video job data for {VideoAssetId}; generated fallback {CorrelationId}",
                    videoAssetId,
                    correlationId);
            }

            using IDisposable? logScope = _logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["CorrelationId"] = correlationId,
                ["VideoAssetId"] = videoAssetId,
            });
            using Activity? activity = VideoProcessingTelemetry.ActivitySource.StartActivity(
                "video.processing.job",
                ActivityKind.Consumer);
            activity?.SetTag("video.asset.id", videoAssetId);
            activity?.SetTag("correlation.id", correlationId);

            int attemptNumber = int.TryParse(
                jobDataMap["AttemptNumber"]?.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsedAttempt)
                ? parsedAttempt
                : 1;

            _logger.LogInformation(
                "Starting video processing job attempt {AttemptNumber} for video asset {VideoAssetId}",
                attemptNumber,
                videoAssetId);

            try
            {
                var mediaAssetResult = await _mediaAssetsRepository.GetBy(
                    m => m.Id == videoAssetId, cancellationToken);

                if (mediaAssetResult.IsFailure)
                {
                    _logger.LogError("Video asset {VideoAssetId} not found", videoAssetId);
                    var orphanedProcess = await _videoProcessesRepository.GetBy(
                        process => process.VideoAssetId == videoAssetId,
                        cancellationToken);
                    if (orphanedProcess.IsSuccess)
                    {
                        orphanedProcess.Value.MarkAsPermanentlyFailed("Video asset not found");
                        await _transactionManager.SaveChangeAsync(cancellationToken);
                    }

                    await context.Scheduler.DeleteJob(context.JobDetail.Key, cancellationToken);
                    return;
                }

                var mediaAsset = mediaAssetResult.Value;

                if (mediaAsset.Status != MediaStatus.UPLOADED && mediaAsset.Status != MediaStatus.PROCESSING)
                {
                    _logger.LogWarning(
                        "Video asset {VideoAssetId} cannot be processed in status {MediaStatus}; skipping job",
                        videoAssetId, mediaAsset.Status);

                    var invalidAssetProcess = await _videoProcessesRepository.GetBy(
                        process => process.VideoAssetId == videoAssetId,
                        cancellationToken);
                    if (invalidAssetProcess.IsSuccess)
                    {
                        invalidAssetProcess.Value.MarkAsPermanentlyFailed(
                            $"Video asset status {mediaAsset.Status} does not allow processing");
                        await _transactionManager.SaveChangeAsync(cancellationToken);
                    }

                    await DeleteJobAsync(context);
                    return;
                }

                // Проверяем текущий VideoProcess (если есть)
                var existingProcess = await _videoProcessesRepository.GetBy(
                    v => v.VideoAssetId == videoAssetId, cancellationToken);

                if (existingProcess.IsSuccess)
                {
                    var process = existingProcess.Value;

                    // Уже обработан
                    if (process.Status == VideoProcessStatus.SUCCEEDED)
                    {
                        _logger.LogInformation("Video asset {VideoAssetId} is already processed", videoAssetId);
                        await DeleteJobAsync(context);
                        return;
                    }

                    // Проверяем, не критическая ли ошибка
                    if (process.IsCriticalError)
                    {
                        _logger.LogError(
                            "Video asset {VideoAssetId} has a permanent processing failure; retry is not allowed",
                            videoAssetId);
                        await DeleteJobAsync(context);
                        return;
                    }

                    if (process.Status == VideoProcessStatus.RUNNING)
                    {
                        _logger.LogWarning(
                            "Recovering interrupted video processing execution for {VideoAssetId}",
                            videoAssetId);
                        var failInterruptedResult = process.Fail(
                            "Previous video processing execution was interrupted",
                            isCritical: false);
                        if (failInterruptedResult.IsFailure)
                        {
                            process.MarkAsPermanentlyFailed(failInterruptedResult.Error.Message);
                        }
                    }

                    if (process.Status == VideoProcessStatus.FAILED)
                    {
                        _logger.LogInformation(
                            "Preparing retry {RetryNumber} for video asset {VideoAssetId}",
                            process.RetryCount + 1,
                            videoAssetId);

                        var prepareResult = process.PrepareForRetry();
                        if (prepareResult.IsFailure)
                        {
                            _logger.LogError(
                                "Failed to prepare retry for video asset {VideoAssetId}. Error code: {ErrorCode}",
                                videoAssetId,
                                prepareResult.Error.Code);
                            process.MarkAsPermanentlyFailed(prepareResult.Error.Message);
                            await _transactionManager.SaveChangeAsync(cancellationToken);
                            await DeleteJobAsync(context);
                            return;
                        }

                        await _transactionManager.SaveChangeAsync(cancellationToken);
                    }
                }

                // Запускаем Pipeline (он сам создаст/загрузит VideoProcess)
                var result = await _videoProcessingService.ProcessVideoAsync(videoAssetId, cancellationToken);

                if (result.IsSuccess)
                {
                    await DeleteJobAsync(context);
                }
                else
                {
                    await HandleFailureAsync(
                        context, videoAssetId, result.Error, attemptNumber, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing video {VideoAssetId}", videoAssetId);
                await HandleExceptionAsync(context, videoAssetId, ex, attemptNumber, cancellationToken);
            }
        }

        private async Task HandleFailureAsync(
            IJobExecutionContext context,
            Guid videoAssetId,
            Error error,
            int attemptNumber,
            CancellationToken cancellationToken)
        {
            _logger.LogError(
                "Video processing job attempt {AttemptNumber} failed for video asset {VideoAssetId}. Error code: {ErrorCode}",
                attemptNumber,
                videoAssetId,
                error.Code);

            // Загружаем процесс (Pipeline уже сохранил статус FAILED)
            var processResult = await _videoProcessesRepository.GetBy(
                v => v.VideoAssetId == videoAssetId, cancellationToken);

            if (processResult.IsFailure)
            {
                _logger.LogError("Video process not found for {VideoAssetId}", videoAssetId);
                await DeleteJobAsync(context);
                return;
            }

            var process = processResult.Value;

            // Final domain state may already be prepared in the DbContext when the first SaveChanges failed.
            // Retry that atomic save (asset + process + Wolverine outbox) before creating another processing retry.
            if (process.Status == VideoProcessStatus.SUCCEEDED)
            {
                var saveFinalStateResult = await _transactionManager.SaveChangeAsync(cancellationToken);
                if (saveFinalStateResult.IsSuccess)
                {
                    _logger.LogInformation(
                        "Persisted previously prepared final state for video {VideoAssetId}",
                        videoAssetId);
                    await DeleteJobAsync(context);
                }
                else
                {
                    _logger.LogError(
                        "Failed to persist prepared final state for video asset {VideoAssetId}. Error code: {ErrorCode}",
                        videoAssetId,
                        saveFinalStateResult.Error.Code);
                }

                return;
            }

            // Проверяем, можно ли повторить
            if (!process.CanRetry())
            {
                _logger.LogWarning(
                    "Cannot retry video asset {VideoAssetId}. Permanent: {IsPermanent}; retry count: {RetryCount}/{MaxRetries}",
                    videoAssetId, process.IsCriticalError, process.RetryCount, process.MaxRetries);

                process.MarkAsPermanentlyFailed(error.Message);
                await _transactionManager.SaveChangeAsync(cancellationToken);
                await DeleteJobAsync(context);
                return;
            }

            // Планируем повтор
            await ScheduleRetryAsync(context, process, attemptNumber, cancellationToken);
        }

        private async Task HandleExceptionAsync(
            IJobExecutionContext context,
            Guid videoAssetId,
            Exception exception,
            int attemptNumber,
            CancellationToken cancellationToken)
        {
            var processResult = await _videoProcessesRepository.GetBy(
                v => v.VideoAssetId == videoAssetId, cancellationToken);

            if (processResult.IsFailure)
            {
                _logger.LogError("Video process not found for {VideoAssetId}", videoAssetId);
                await DeleteJobAsync(context);
                return;
            }

            var process = processResult.Value;

            // Неожиданная ошибка - считаем временной (не критической)
            process.Fail($"Unexpected error: {exception.Message}", isCritical: false);
            await _transactionManager.SaveChangeAsync(cancellationToken);

            await HandleFailureAsync(
                context, videoAssetId,
                Error.Failure("unexpected", exception.Message),
                attemptNumber, cancellationToken);
        }

        private async Task ScheduleRetryAsync(
            IJobExecutionContext context,
            VideoProcess process,
            int currentAttempt,
            CancellationToken cancellationToken)
        {
            var delay = _processingPolicy.GetRetryDelay(process.RetryCount);
            var nextRetryTime = DateTimeOffset.UtcNow.Add(delay);
            int nextAttempt = currentAttempt + 1;

            var plannedResult = process.PlannedRetry(nextRetryTime.UtcDateTime);
            if (plannedResult.IsFailure)
            {
                _logger.LogError(
                    "Failed to plan retry for video asset {VideoAssetId}. Error code: {ErrorCode}",
                    process.VideoAssetId,
                    plannedResult.Error.Code);
                process.MarkAsPermanentlyFailed(plannedResult.Error.Message);
                await _transactionManager.SaveChangeAsync(cancellationToken);
                await DeleteJobAsync(context);
                return;
            }

            await _transactionManager.SaveChangeAsync(cancellationToken);

            _logger.LogInformation(
                "Scheduling retry {NextRetryNumber} for video asset {VideoAssetId} after {RetryDelay} at {NextRetryAt}",
                nextAttempt, process.VideoAssetId, delay, nextRetryTime);

            var retryTrigger = TriggerBuilder.Create()
                .WithIdentity($"Retry_{process.VideoAssetId}_{nextAttempt}_{Guid.NewGuid():N}",
                    VideoProcessingScheduler.GROUP_NAME)
                .ForJob(context.JobDetail.Key)
                .StartAt(nextRetryTime)
                .WithSimpleSchedule(schedule => schedule.WithMisfireHandlingInstructionFireNow())
                .UsingJobData("VideoAssetId", process.VideoAssetId.ToString())
                .UsingJobData("AttemptNumber", nextAttempt.ToString(CultureInfo.InvariantCulture))
                .UsingJobData("CorrelationId", process.CorrelationId)
                .Build();

            await context.Scheduler.ScheduleJob(retryTrigger, cancellationToken);
            _telemetry.RetriedJobs.Add(1);
        }

        private async Task DeleteJobAsync(IJobExecutionContext context)
        {
            try
            {
                await context.Scheduler.DeleteJob(context.JobDetail.Key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete Quartz job {JobKey}", context.JobDetail.Key);
            }
        }

        private static bool TryGetGuidFromJobData(JobDataMap jobDataMap, string key, out Guid result)
        {
            result = Guid.Empty;

            if (!jobDataMap.ContainsKey(key))
                return false;

            string? value = jobDataMap.GetString(key);
            if (string.IsNullOrEmpty(value))
                return false;

            return Guid.TryParse(value, out result);
        }
    }
