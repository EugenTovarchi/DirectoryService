using System.Diagnostics;
using CSharpFunctionalExtensions;
using FileService.Contracts.Messaging.Events;
using FileService.Core.Abstractions;
using FileService.Domain;
using FileService.Domain.MediaProcessing;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;
using Wolverine;

namespace FileService.VideoProcessing.Pipeline;

public class ProcessingPipeline : IProcessingPipeline
{
    private readonly IEnumerable<IProcessingStepHandler> _stepHandlers;
    private readonly ILogger<ProcessingPipeline> _logger;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;
    private readonly IVideoProcessesRepository _videoProcessesRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IProcessingErrorClassifier _errorClassifier;
    private readonly VideoProcessingTelemetry _telemetry;
    private readonly IMessageBus _messageBus;

    public ProcessingPipeline(
        IEnumerable<IProcessingStepHandler> stepHandlers,
        ILogger<ProcessingPipeline> logger,
        IMediaAssetsRepository mediaAssetsRepository,
        IVideoProcessesRepository videoProcessesRepository,
        ITransactionManager transactionManager,
        IProcessingErrorClassifier errorClassifier,
        VideoProcessingTelemetry telemetry,
        IMessageBus messageBus)
    {
        _stepHandlers = stepHandlers;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
        _videoProcessesRepository = videoProcessesRepository;
        _transactionManager = transactionManager;
        _errorClassifier = errorClassifier;
        _telemetry = telemetry;
        _messageBus = messageBus;
    }

    public async Task<UnitResult<Error>> ProcessAllStepsAsync(
        Guid videoAssetId,
        CancellationToken cancellationToken = default)
    {
        Result<ProcessingContext, Error> contextResult = await LoadContextAsync(videoAssetId, cancellationToken);
        if (contextResult.IsFailure)
            return contextResult.Error;

        ProcessingContext processingContext = contextResult.Value;

        UnitResult<Error> allStepExecutionResult = await ExecuteAllStepsAsync(processingContext, cancellationToken);
        if (allStepExecutionResult.IsFailure)
        {
            return await FinalizeWithFailureAsync(processingContext, allStepExecutionResult.Error, cancellationToken);
        }

        UnitResult<Error> finalizeResult = await FinalizeAsync(processingContext, cancellationToken);
        if (finalizeResult.IsFailure && processingContext.VideoProcess.Status == VideoProcessStatus.RUNNING)
            return await FinalizeWithFailureAsync(processingContext, finalizeResult.Error, cancellationToken);

        return finalizeResult;
    }

    private async Task<UnitResult<Error>> ExecuteAllStepsAsync(ProcessingContext processingContext,
        CancellationToken cancellationToken)
    {
        Guid videoAssetId = processingContext.VideoProcess.VideoAssetId;
        while (true)
        {
            Result<VideoProcessStep?, Error> stepResult = processingContext.VideoProcess.ProcessNextStep();
            if (stepResult.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to determine next step {StepName} for video asset {VideoAssetId}. Error code: {ErrorCode}",
                    processingContext.VideoProcess.CurrentStep?.Name,
                    videoAssetId,
                    stepResult.Error.Code);
                return stepResult.Error;
            }

            if (stepResult.Value is null)
            {
                _logger.LogDebug("All processing steps completed for video asset {VideoAssetId}", videoAssetId);
                return UnitResult.Success<Error>();
            }

            VideoProcessStep? currentStep = stepResult.Value;

            _logger.LogInformation(
                "Processing step {StepName} with order {StepOrder} for video asset {VideoAssetId}",
                currentStep.Name, currentStep.Order, videoAssetId);

            IProcessingStepHandler? stepHandler = _stepHandlers
                .FirstOrDefault(s => string.Equals(s.StepName.ToString(), currentStep.Name, StringComparison.OrdinalIgnoreCase));
            if (stepHandler is null)
            {
                string error = $"No step handler registered for this step: {currentStep.Name}";
                Error handlerError = Error.NotFound("pipeline.handler.not.found", error);
                _logger.LogError(
                    "No handler is registered for step {StepName} of video asset {VideoAssetId}",
                    currentStep.Name,
                    videoAssetId);

                processingContext.VideoProcess.Fail(error, isCritical: _errorClassifier.IsCritical(handlerError));
                var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
                if (saveResult.IsFailure)
                {
                    _logger.LogError(
                        "Failed to persist missing-handler state for step {StepName} of video asset {VideoAssetId}. Error code: {ErrorCode}",
                        currentStep.Name,
                        videoAssetId,
                        saveResult.Error.Code);
                }

                return handlerError;
            }

            Result<ProcessingContext, Error> executionResult = await ExecuteStepSafelyAsync(
                stepHandler, processingContext, cancellationToken);
            if (executionResult.IsFailure)
            {
                _logger.LogWarning(
                    "Step {StepName} failed for video asset {VideoAssetId}. Error code: {ErrorCode}",
                    currentStep.Name,
                    videoAssetId,
                    executionResult.Error.Code);

                processingContext.VideoProcess.Fail(
                    executionResult.Error.Message,
                    isCritical: _errorClassifier.IsCritical(executionResult.Error));

                var saveErrorResult = await _transactionManager.SaveChangeAsync(cancellationToken);
                if (saveErrorResult.IsFailure)
                {
                    _logger.LogError(
                        "Failed to persist failed step {StepName} for video asset {VideoAssetId}. Error code: {ErrorCode}",
                        currentStep.Name,
                        videoAssetId,
                        saveErrorResult.Error.Code);
                }

                return executionResult.Error;
            }

            UnitResult<Error> completeStepResult =
                processingContext.VideoProcess.CompleteStep(processingContext.VideoProcess.CurrentStep!.Order);
            if (completeStepResult.IsFailure)
                return completeStepResult.Error;

            _logger.LogInformation(
                "Completed step {StepName} for video asset {VideoAssetId}. Progress: {ProgressPercent}%",
                currentStep.Name, videoAssetId, processingContext.VideoProcess.TotalProgress);

            var completeSaveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
            if (completeSaveResult.IsFailure)
            {
                _logger.LogError(
                    "Failed to persist progress after step {StepName} for video asset {VideoAssetId}. Error code: {ErrorCode}",
                    currentStep.Name,
                    videoAssetId,
                    completeSaveResult.Error.Code);
                return completeSaveResult.Error;
            }
        }
    }

    private async Task<Result<ProcessingContext, Error>> LoadContextAsync(
        Guid videoAssetId,
        CancellationToken cancellationToken = default)
    {
        var videoAssetResult = await _mediaAssetsRepository.GetVideoBy(va => va.Id == videoAssetId,
            cancellationToken);
        if (videoAssetResult.IsFailure)
            return videoAssetResult.Error;

        VideoProcess videoProcess;
        bool shouldStartStep = false;

        var processResult =
            await _videoProcessesRepository.GetBy(v => v.VideoAssetId == videoAssetId, cancellationToken);
        if (processResult.IsFailure)
        {
            var newProcess = VideoProcess.Create(videoAssetId, videoAssetResult.Value.RawKey!);
            if (newProcess.IsFailure)
                return newProcess.Error;

            videoProcess = newProcess.Value;
            shouldStartStep = true;

            _videoProcessesRepository.Add(videoProcess);

            _logger.LogInformation("Created video process for video asset {VideoAssetId}", videoAssetId);
        }
        else
        {
            videoProcess = processResult.Value;
            _logger.LogInformation("Attached existing video process for video asset {VideoAssetId}", videoAssetId);

            if (videoProcess.Status == VideoProcessStatus.FAILED && videoProcess.CanRetry())
            {
                _logger.LogInformation("Preparing failed process for retry for video asset {VideoAssetId}", videoAssetId);
                var prepareResult = videoProcess.PrepareForRetry();
                if (prepareResult.IsFailure)
                    return prepareResult.Error;

                shouldStartStep = true;
            }
            else if (videoProcess.Status == VideoProcessStatus.PENDING)
            {
                shouldStartStep = true;
            }
        }

        if (videoAssetResult.Value.Status == MediaStatus.UPLOADED)
        {
            var startResult = videoAssetResult.Value.StartProcessing();
            if (startResult.IsFailure)
                return startResult.Error;
        }
        else if (videoAssetResult.Value.Status != MediaStatus.PROCESSING)
        {
            return Error.Validation("asset.invalid.status",
                $"Video asset must be UPLOADED or PROCESSING, current: {videoAssetResult.Value.Status}");
        }

        if (shouldStartStep)
        {
            VideoProcessStep? nextStep = videoProcess.Steps
                .OrderBy(s => s.Order)
                .FirstOrDefault(s => s.Status == VideoProcessStatus.PENDING);
            if (nextStep is null)
                return Error.NotFound("steps.not.found", "No pending steps defined for video process");

            UnitResult<Error> startNewProcessResult = videoProcess.StartStep(nextStep.Order, nextStep.Name);
            if (startNewProcessResult.IsFailure)
                return startNewProcessResult.Error;

            _logger.LogDebug(
                "Started step {StepName} for video asset {VideoAssetId}",
                nextStep.Name,
                videoAssetId);
        }

        var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        ProcessingContext processingContext = new()
        {
            VideoAsset = videoAssetResult.Value, VideoProcess = videoProcess
        };

        return processingContext;
    }

    private async Task<Result<ProcessingContext, Error>> ExecuteStepSafelyAsync(
        IProcessingStepHandler step, ProcessingContext context, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using Activity? activity = VideoProcessingTelemetry.ActivitySource.StartActivity(
            $"video.processing.step.{step.StepName}");
        activity?.SetTag("video.asset.id", context.VideoAsset.Id);
        activity?.SetTag("video.processing.step", step.StepName);

        Result<ProcessingContext, Error> result;
        try
        {
            result = await step.ExecuteAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in step handler {StepName} for video asset: {VideoAssetId}",
                step.StepName, context.VideoAsset.Id);
            result = Error.Failure("pipeline.step.exception", ex.Message);
        }

        stopwatch.Stop();
        string status = result.IsSuccess ? "succeeded" : "failed";
        activity?.SetStatus(
            result.IsSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error,
            result.IsFailure ? result.Error.Code : null);
        _telemetry.StepDuration.Record(
            stopwatch.Elapsed.TotalSeconds,
            new KeyValuePair<string, object?>("step", step.StepName),
            new KeyValuePair<string, object?>("status", status));

        return result;
    }

    private async Task<UnitResult<Error>> FinalizeWithFailureAsync(
        ProcessingContext context, Error error, CancellationToken cancellationToken)
    {
        Guid videoAssetId = context.VideoProcess.VideoAssetId;

        if (context.VideoProcess.Status == VideoProcessStatus.RUNNING)
        {
            context.VideoProcess.Fail(
                error.GetMessage(),
                isCritical: _errorClassifier.IsCritical(error));
        }

        _logger.LogWarning(
            "Video processing failed for video asset {VideoAssetId}. Error code: {ErrorCode}",
            videoAssetId,
            error.Code);

        var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return UnitResult.Failure(error);
    }

    private async Task<UnitResult<Error>> FinalizeAsync(
        ProcessingContext context, CancellationToken cancellationToken)
    {
        Guid videoAssetId = context.VideoProcess.VideoAssetId;

        if (context.VideoProcess.HlsKey is null)
            return Error.Failure("processing.hls.key.missing", "HLS key is missing after video processing");

        if (context.VideoProcess.Status != VideoProcessStatus.RUNNING
            || context.VideoProcess.Steps.Any(step => step.Status != VideoProcessStatus.SUCCEEDED))
        {
            return Error.Failure(
                "processing.not.completed",
                "Video process cannot be finalized before all steps succeed");
        }

        if (context.VideoAsset.Status != MediaStatus.PROCESSING)
        {
            return Error.Failure(
                "asset.invalid.status.transition",
                $"Video asset must be PROCESSING before finalization, current: {context.VideoAsset.Status}");
        }

        var videoReadyEvent = new VideoReady(
            context.VideoAsset.Id,
            context.VideoAsset.OwnerId,
            context.VideoAsset.OwnerType,
            context.VideoProcess.HlsKey.Value,
            context.VideoProcess.CorrelationId,
            DateTimeOffset.UtcNow);

        _logger.LogDebug(
            "Publishing VideoReady event for video asset {VideoAssetId} with correlation {CorrelationId}",
            videoAssetId,
            context.VideoProcess.CorrelationId);
        try
        {
            await _messageBus.PublishAsync(videoReadyEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish VideoReady event for video asset {VideoAssetId}", videoAssetId);
            return Error.Failure("video.ready.publish.failed", ex.Message);
        }

        UnitResult<Error> finishProcessResult = context.VideoProcess.FinishProcessing();
        if (finishProcessResult.IsFailure)
            return finishProcessResult.Error;

        UnitResult<Error> completeAssetResult = context.VideoAsset.CompleteProcessing();
        if (completeAssetResult.IsFailure)
            return completeAssetResult.Error;

        var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
        {
            _logger.LogError("Failed to save final state for video asset: {VideoAssetId}", videoAssetId);
            return saveResult.Error;
        }

        _logger.LogInformation(
            "Persisted final video state and VideoReady outbox event for video asset {VideoAssetId}",
            videoAssetId);

        return UnitResult.Success<Error>();
    }
}
