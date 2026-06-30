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
        _logger.LogDebug("Initializing processing context for video asset {VideoAssetId}",
            context.VideoProcess.VideoAssetId);

        var createWorkdirResult = context.CreateWorkingDirectory();
        if (createWorkdirResult.IsFailure)
            return Task.FromResult(Result.Failure<ProcessingContext, Error>(createWorkdirResult.Error));

        _logger.LogDebug("Created working directory for video asset {VideoAssetId}",
            context.VideoProcess.VideoAssetId);

        return Task.FromResult(Result.Success<ProcessingContext, Error>(context));
    }
}
