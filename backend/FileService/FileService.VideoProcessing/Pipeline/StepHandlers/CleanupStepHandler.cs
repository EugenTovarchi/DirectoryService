using CSharpFunctionalExtensions;
using FileService.Core.FilesStorage;
using FileService.Domain.MediaProcessing;
using FileService.VideoProcessing.Pipeline.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline.StepHandlers;

public sealed class CleanupStepHandler : IProcessingStepHandler
{
    private readonly ILogger<CleanupStepHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;

    public CleanupStepHandler(
        ILogger<CleanupStepHandler> logger,
        IFileStorageProvider fileStorageProvider,
        IOptions<VideoProcessingOptions> options)
    {
        _logger = logger;
        _fileStorageProvider = fileStorageProvider;
    }

    public string StepName => StepNames.Cleanup;

    public async Task<Result<ProcessingContext, Error>> ExecuteAsync(ProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Cleaning temporary files for video asset {VideoAssetId}",
            context.VideoProcess.VideoAssetId);

        if (string.IsNullOrWhiteSpace(context.WorkingDirectory))
        {
            _logger.LogWarning("Working directory is not set, skipping cleanup");
            return await Task.FromResult(context);
        }

        Result<string, Error> deleteResult = await _fileStorageProvider.DeleteFileAsync(context.VideoAsset.RawKey!,
            cancellationToken);
        if (deleteResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to delete raw file from storage for video asset {VideoAssetId}. Error code: {ErrorCode}",
                context.VideoProcess.VideoAssetId,
                deleteResult.Error.Code);
        }
        else
        {
            _logger.LogDebug("Raw file deleted from storage for video asset: {VideoAssetId}",
                context.VideoProcess.VideoAssetId);
        }

        try
        {
            if (Directory.Exists(context.WorkingDirectory))
            {
                Directory.Delete(context.WorkingDirectory, true);
                _logger.LogDebug("Deleted working directory for video asset {VideoAssetId}",
                    context.VideoProcess.VideoAssetId);

                context.Cleanup();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete working directory {WorkingDirectory}; it will be cleaned later",
                context.WorkingDirectory);
        }

        return await Task.FromResult(context);
    }
}
