using CSharpFunctionalExtensions;
using FileService.Core.FilesStorage;
using FileService.Domain.MediaProcessing;
using FileService.VideoProcessing.Preview;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline.StepHandlers;

public sealed class GeneratePreviewStepHandler : IProcessingStepHandler
{
    private readonly ILogger<GeneratePreviewStepHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IPreviewCalculator _previewCalculator;
    private readonly IPreviewUploader _previewUploader;

    public GeneratePreviewStepHandler(
        ILogger<GeneratePreviewStepHandler> logger,
        IFileStorageProvider fileStorageProvider,
        IPreviewCalculator previewCalculator,
        IPreviewUploader previewUploader)
    {
        _logger = logger;
        _fileStorageProvider = fileStorageProvider;
        _previewCalculator = previewCalculator;
        _previewUploader = previewUploader;
    }

    public string StepName => StepNames.GeneratePreview;

    public async Task<Result<ProcessingContext, Error>> ExecuteAsync(
        ProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating previews for video asset {VideoAssetId}",
            context.VideoProcess.VideoAssetId);

        if (string.IsNullOrEmpty(context.MediaAssetUrl))
        {
            var downloadUrl = await _fileStorageProvider
                .GenerateDownloadUrlAsync(context.VideoAsset.UploadKey, cancellationToken);
            if (downloadUrl.IsFailure)
                return downloadUrl.Error;
            context.SetMediaAssetUrl(downloadUrl.Value);
        }

        var metadata = context.VideoProcess.MetaData;
        if (metadata == null)
        {
            return Error.Failure("preview.metadata.missing",
                "Video metadata not available. Ensure ExtractMetadata step completed successfully.");
        }

        var timestamps = _previewCalculator.CalculateExtractionTimes(metadata.Duration);
        if (timestamps.Count == 0)
        {
            _logger.LogWarning("No preview timestamps were calculated for video duration {VideoDuration}",
                metadata.Duration);
            return context;
        }

        if (string.IsNullOrWhiteSpace(context.WorkingDirectory))
        {
            return Error.Failure("preview.workdir.missing",
                "Working directory not initialized. Ensure Initialize step completed successfully.");
        }

        var uploadResult = await _previewUploader.GenerateAndUploadPreviewsAsync(
            context.MediaAssetUrl!,
            context.WorkingDirectory,
            context.VideoAsset.Id,
            timestamps,
            cancellationToken);

        if (uploadResult.IsFailure)
            return uploadResult.Error;

        context.SetPreviewKeys(uploadResult.Value.PreviewKeys, uploadResult.Value.SpriteKey);

        _logger.LogInformation(
            "Generated {PreviewCount} previews for video asset {VideoAssetId}. Sprite sheet created: {HasSpriteSheet}",
            uploadResult.Value.PreviewKeys.Count,
            context.VideoProcess.VideoAssetId,
            uploadResult.Value.SpriteKey != null);

        return context;
    }
}
