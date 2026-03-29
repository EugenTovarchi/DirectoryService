using CSharpFunctionalExtensions;
using FileService.Core;
using FileService.Core.Abstractions;
using FileService.Domain.MediaProcessing;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline;

public class ProcessingPipeline : IProcessingPipeline
{
    private readonly IEnumerable<IProcessingStepHandler> _stepHandlers;
    private readonly ILogger<ProcessingPipeline> _logger;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;
    private readonly IVideoProcessesRepository _videoProcessesRepository;
    private readonly ITransactionManager _transactionManager;

    public ProcessingPipeline(
        IEnumerable<IProcessingStepHandler> stepHandlers,
        ILogger<ProcessingPipeline> logger,
        IMediaAssetsRepository mediaAssetsRepository,
        IVideoProcessesRepository videoProcessesRepository,
        ITransactionManager transactionManager)
    {
        _stepHandlers = stepHandlers;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
        _videoProcessesRepository = videoProcessesRepository;
        _transactionManager = transactionManager;
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

        return await FinalizeAsync(processingContext, cancellationToken);
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
                _logger.LogWarning("Failed processing step {Step} of video asset{videoAssetId}: {Error}",
                    stepResult.Error, videoAssetId, stepResult.Error.Message);
                return stepResult.Error;
            }

            if (stepResult.Value is null)
            {
                _logger.LogInformation("All steps processed for  video asset {VideoAssetId}", videoAssetId);
                return UnitResult.Success<Error>();
            }

            VideoProcessStep? currentStep = stepResult.Value;

            _logger.LogInformation("Processing step {name} (Order:{Order} for video asset : {videoAssetId})",
                currentStep.Name, currentStep.Order, videoAssetId);

            IProcessingStepHandler? stepHandler = _stepHandlers
                .FirstOrDefault(s => s.StepName.ToString() == currentStep.Name);
            if (stepHandler is null)
            {
                string error = $"No step handler registered for this step: {currentStep.Name}";
                _logger.LogError("No step handler registered for this step: {currentStep.Name}", currentStep.Name);

                processingContext.VideoProcess.Fail(error);
                var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
                if (saveResult.IsFailure)
                {
                    _logger.LogError("Failed to save context after missing handler for step {stepName}" +
                                     " for video asset:{videoAssetId}", currentStep.Name, currentStep.Name);
                }

                return Error.NotFound("pipeline.handler.not.found", error);
            }

            Result<ProcessingContext, Error> executionResult = await ExecuteStepSafelyAsync(
                stepHandler, processingContext, cancellationToken);
            if (executionResult.IsFailure)
            {
                _logger.LogError("Step {stepName} failed for  video asset {videoAssetId}. Error: {error}",
                    currentStep.Name,
                    videoAssetId,
                    executionResult.Error);

                processingContext.VideoProcess.Fail(executionResult.Error.Message, isCritical: true);

                var saveErrorResult = await _transactionManager.SaveChangeAsync(cancellationToken);
                if (saveErrorResult.IsFailure)
                {
                    _logger.LogError("Failed to save context after missing handler for step {stepName}" +
                                     " for video asset:{videoAssetId}", currentStep.Name, currentStep.Name);
                }

                return executionResult.Error;
            }

            processingContext.VideoProcess.CompleteStep(processingContext.VideoProcess.CurrentStep!.Order);

            _logger.LogInformation("Step {stepName} completed for VideoAssetId: {VideoAssetId}. Progress: {Progress}%",
                currentStep.Name, videoAssetId, processingContext.VideoProcess.TotalProgress);

            var completeSaveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
            if (completeSaveResult.IsFailure)
            {
                _logger.LogError("Failed to save progress after step {stepName} for VideoAssetId: {VideoAssetId}",
                    currentStep.Name, videoAssetId);
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
        bool isNewProcess = false;

        var processResult =
            await _videoProcessesRepository.GetBy(v => v.VideoAssetId == videoAssetId, cancellationToken);
        if (processResult.IsFailure)
        {
            var newProcess = VideoProcess.Create(videoAssetId, videoAssetResult.Value.RawKey!);
            if (newProcess.IsFailure)
                return newProcess.Error;

            videoProcess = newProcess.Value;
            isNewProcess = true;

            _videoProcessesRepository.Add(videoProcess);

            _logger.LogInformation("Created new video process for VideoAssetId: {VideoAssetId}", videoAssetId);
        }
        else
        {
            videoProcess = processResult.Value;
            _logger.LogInformation("Attach existing VideoProcess for VideoAssetId: {VideoAssetId}", videoAssetId);

            if (videoProcess.Status == VideoProcessStatus.FAILED && videoProcess.CanRetry())
            {
                _logger.LogInformation("Resetting failed process for VideoAssetId: {VideoAssetId}", videoAssetId);
                var resetResult = videoProcess.Reset();
                if (resetResult.IsFailure)
                    return resetResult.Error;
                isNewProcess = true;
            }
        }

        var startResult = videoAssetResult.Value.StartProcessing();
        if (startResult.IsFailure)
            return startResult.Error;

        if (isNewProcess)
        {
            VideoProcessStep? firstStep = videoProcess.Steps.OrderBy(s => s.Order).FirstOrDefault();
            if (firstStep is null)
                return Error.NotFound("steps.not.found", "No steps defined for video process");

            UnitResult<Error> startNewProcessResult = videoProcess.StartStep(firstStep.Order, firstStep.Name);
            if (startNewProcessResult.IsFailure)
                return startNewProcessResult.Error;

            _logger.LogInformation("Started new video process for VideoAssetId: {VideoAssetId}", videoAssetId);
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
        try
        {
            return await step.ExecuteAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in step handler {stepName} for video asset: {videoAssetId}",
                step.StepName, context.VideoAsset.Id);
            return Error.Failure("pipeline.step.exception", ex.Message);
        }
    }

    private async Task<UnitResult<Error>> FinalizeWithFailureAsync(
        ProcessingContext context, Error error, CancellationToken cancellationToken)
    {
        Guid videoAssetId = context.VideoProcess.VideoAssetId;

        context.VideoProcess.Fail(error.GetMessage());

        _logger.LogError("Video processing failed for video asset: {videoAssetId}. Error: {Error}.",
            videoAssetId, error.GetMessage());

        var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return UnitResult.Failure(error);
    }

    private async Task<UnitResult<Error>> FinalizeAsync(
        ProcessingContext context, CancellationToken cancellationToken)
    {
        Guid videoAssetId = context.VideoProcess.VideoAssetId;

        context.VideoProcess.CompleteStep(context.VideoProcess.CurrentStep!.Order);
        context.VideoAsset.CompleteProcessing();

        _logger.LogInformation("Video processing complete successfully for video asset: {videoAssetId}",
            videoAssetId);

        var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
        {
            _logger.LogError("Failed to save final state for video asset: {videoAssetId}", videoAssetId);
            return saveResult.Error;
        }

        return UnitResult.Success<Error>();
    }
}