using CSharpFunctionalExtensions;
using FileService.Domain.MediaProcessing;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline.StepHandlers;

public sealed class InitializeStepHandler : IProcessingStepHandler
{
    private readonly ILogger<InitializeStepHandler> _logger;

    public InitializeStepHandler(ILogger<InitializeStepHandler> logger)
    {
        _logger = logger;
    }

    public string StepName => StepNames.Initialize;

    public Task<Result<ProcessingContext, Error>> ExecuteAsync(ProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing video processing for video asset: {VideoAsset}",
            context.VideoProcess.VideoAssetId);

        var createWorkdirResult = context.CreateWorkingDirectory();
        if (createWorkdirResult.IsFailure)
            return Task.FromResult(Result.Failure<ProcessingContext, Error>(createWorkdirResult.Error));

        _logger.LogDebug("Workdir created: {Workdir}", context.WorkingDirectory);

        return Task.FromResult(Result.Success<ProcessingContext, Error>(context));
    }
}

public sealed class GeneratePreviewStepHandler : IProcessingStepHandler
{
    public string StepName => StepNames.GeneratePreview;

    public Task<Result<ProcessingContext, Error>> ExecuteAsync(ProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}