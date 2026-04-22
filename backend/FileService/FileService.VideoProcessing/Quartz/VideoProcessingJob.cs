    using FileService.Core.Abstractions;
    using FileService.Domain;
    using FileService.Domain.MediaProcessing;
    using FileService.VideoProcessing.Pipeline;
    using Microsoft.Extensions.Logging;
    using Quartz;
    using SharedService.SharedKernel;

    namespace FileService.VideoProcessing.Quartz;

    public class VideoProcessingJob : IJob
    {
        private readonly IVideoProcessingService _videoProcessingService;
        private readonly IVideoProcessesRepository _videoProcessesRepository;
        private readonly IMediaAssetsRepository _mediaAssetsRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly ILogger<VideoProcessingJob> _logger;

        public VideoProcessingJob(
            IVideoProcessingService videoProcessingService,
            IMediaAssetsRepository mediaAssetsRepository,
            ILogger<VideoProcessingJob> logger,
            ITransactionManager transactionManager,
            IVideoProcessesRepository videoProcessesRepository)
        {
            _videoProcessingService = videoProcessingService;
            _mediaAssetsRepository = mediaAssetsRepository;
            _logger = logger;
            _transactionManager = transactionManager;
            _videoProcessesRepository = videoProcessesRepository;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var cancellationToken = context.CancellationToken;
            var jobDataMap = context.MergedJobDataMap;

            if (!TryGetGuidFromJobData(jobDataMap, "VideoAssetId", out var videoAssetId))
            {
                _logger.LogError("Invalid or missing VideoAssetId in JobDataMap");
                return;
            }

            _logger.LogInformation("Starting processing for video {VideoAssetId}", videoAssetId);

            int attemptNumber = jobDataMap.GetInt("AttemptNumber");
            if (attemptNumber == 0)
            {
                attemptNumber = 1;
            }

            _logger.LogInformation("Attempt: {Attempt} for video {VideoAssetId}", attemptNumber, videoAssetId);

            try
            {
                var mediaAssetResult = await _mediaAssetsRepository.GetBy(
                    m => m.Id == videoAssetId, cancellationToken);

                if (mediaAssetResult.IsFailure)
                {
                    _logger.LogError("Video asset {VideoAssetId} not found", videoAssetId);
                    await context.Scheduler.DeleteJob(context.JobDetail.Key, cancellationToken);
                    return;
                }

                var mediaAsset = mediaAssetResult.Value;

                if (mediaAsset.Status != MediaStatus.UPLOADED && mediaAsset.Status != MediaStatus.PROCESSING)
                {
                    _logger.LogWarning(
                        "Video {VideoAssetId} is not in not ready for processing, status {Status}, skipping",
                        videoAssetId, mediaAsset.Status);

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
                        _logger.LogInformation("Video {VideoAssetId} already processed", videoAssetId);
                        await DeleteJobAsync(context);
                        return;
                    }

                    // Превышен лимит попыток
                    if (process.RetryCount >= process.MaxRetries)
                    {
                        _logger.LogError("Max retries {MaxRetries} exceeded for video {VideoAssetId}",
                            process.MaxRetries, videoAssetId);
                        process.MarkAsPermanentlyFailed("Max retries exceeded");
                        await _transactionManager.SaveChangeAsync(cancellationToken);
                        await DeleteJobAsync(context);
                        return;
                    }

                    // Проверяем, не критическая ли ошибка
                    if (process.IsCriticalError)
                    {
                        _logger.LogError(
                            "Critical error for video {VideoAssetId}, cannot retry",
                            videoAssetId);
                        await DeleteJobAsync(context);
                        return;
                    }
                }

                // Если это повторная попытка и процесс существует - готовим к повтору
                if (attemptNumber > 1 && existingProcess.IsSuccess)
                {
                    var process = existingProcess.Value;
                    _logger.LogInformation("Preparing process for retry: {Attempt}", attemptNumber);

                    var prepareResult = process.PrepareForRetry();
                    if (prepareResult.IsFailure)
                    {
                        _logger.LogError("Failed to prepare for retry: {Error}", prepareResult.Error.Message);
                        process.MarkAsPermanentlyFailed(prepareResult.Error.Message);
                        await _transactionManager.SaveChangeAsync(cancellationToken);
                        await DeleteJobAsync(context);
                        return;
                    }

                    await _transactionManager.SaveChangeAsync(cancellationToken);
                }

                // Запускаем Pipeline (он сам создаст/загрузит VideoProcess)
                var result = await _videoProcessingService.ProcessVideoAsync(videoAssetId, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "Successfully processed video {VideoAssetId} on attempt #{Attempt}",
                        videoAssetId, attemptNumber);
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
                "Attempt: {Attempt} failed for video {VideoAssetId}: {Error}",
                attemptNumber, videoAssetId, error.Message);

            // Загружаем процесс (Pipeline уже сохранил статус FAILED)
            var processResult = await _videoProcessesRepository.GetBy(
                v => v.VideoAssetId == videoAssetId, cancellationToken);

            if (processResult.IsFailure)
            {
                _logger.LogError("Video process not found for {VideoAssetId}", videoAssetId);
                return;
            }

            var process = processResult.Value;

            // Проверяем, можно ли повторить
            if (!process.CanRetry())
            {
                _logger.LogWarning(
                    "Cannot retry video {VideoAssetId} (Critical={Critical}, Retries={RetryCount}/{MaxRetries})",
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
            var delay = process.GetNextRetryDelay();
            var nextRetryTime = DateTimeOffset.UtcNow.Add(delay);
            int nextAttempt = currentAttempt + 1;

            var plannedResult = process.PlannedRetry(nextRetryTime.UtcDateTime);
            if (plannedResult.IsFailure)
            {
                _logger.LogError("Failed to plan retry: {Error}", plannedResult.Error.Message);
                process.MarkAsPermanentlyFailed(plannedResult.Error.Message);
                await _transactionManager.SaveChangeAsync(cancellationToken);
                await DeleteJobAsync(context);
                return;
            }

            await _transactionManager.SaveChangeAsync(cancellationToken);

            _logger.LogInformation(
                "Scheduling retry: {NextAttempt} for video {VideoAssetId} in {Delay} at {Time}",
                nextAttempt, process.VideoAssetId, delay, nextRetryTime);

            var retryTrigger = TriggerBuilder.Create()
                .WithIdentity($"Retry_{process.VideoAssetId}_{nextAttempt}_{Guid.NewGuid():N}")
                .StartAt(nextRetryTime)
                .UsingJobData("VideoAssetId", process.VideoAssetId.ToString())
                .UsingJobData("AttemptNumber", nextAttempt)
                .Build();

            await context.Scheduler.ScheduleJob(retryTrigger, cancellationToken);
            await DeleteJobAsync(context);
        }

        private async Task DeleteJobAsync(IJobExecutionContext context)
        {
            try
            {
                await context.Scheduler.DeleteJob(context.JobDetail.Key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete job");
            }
        }

        private bool TryGetGuidFromJobData(JobDataMap jobDataMap, string key, out Guid result)
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