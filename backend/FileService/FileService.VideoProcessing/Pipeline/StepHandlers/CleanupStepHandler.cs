using CSharpFunctionalExtensions;
using FileService.Core.FilesStorage;
using FileService.Domain.MediaProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline.StepHandlers;

public sealed class CleanupStepHandler : IProcessingStepHandler
{
    private readonly ILogger<CleanupStepHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly VideoProcessingOptions _options;

    public CleanupStepHandler(
        ILogger<CleanupStepHandler> logger,
        IFileStorageProvider fileStorageProvider,
        IOptions<VideoProcessingOptions> options)
    {
        _logger = logger;
        _fileStorageProvider = fileStorageProvider;
        _options = options.Value;
    }

    public string StepName => StepNames.Cleanup;

    public async Task<Result<ProcessingContext, Error>> ExecuteAsync(ProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up temporary files for VideoAsset: {VideoAssetId}",
            context.VideoProcess.VideoAssetId);

        if (string.IsNullOrWhiteSpace(context.WorkingDirectory))
        {
            _logger.LogWarning("Working directory is not set, skipping cleanup");
            return await Task.FromResult(context);
        }

        Result<string, Error> deleteResult = await _fileStorageProvider.DeleteFileAsync(context.VideoAsset.RawKey!, cancellationToken);
        if (deleteResult.IsFailure)
        {
            _logger.LogWarning("Failed to delete raw file from storage for video asset: {VideoAssetId}. Error: {Error}",
                context.VideoProcess.VideoAssetId, deleteResult.Error);
        }
        else
        {
            _logger.LogDebug("Raw file deleted from storage for video asset: {VideoAssetId}",
                context.VideoProcess.VideoAssetId);
        }

        try
        {
            if(Directory.Exists(context.WorkingDirectory))
            {
                Directory.Delete(context.WorkingDirectory, true);
                _logger.LogDebug("Directory deleted: {Directory}", context.WorkingDirectory);

                context.Cleanup();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete working directory: {directory}. Will be cleaned later",
                context.WorkingDirectory);
        }

        return await Task.FromResult(context);
    }
}