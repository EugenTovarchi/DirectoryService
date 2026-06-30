using CSharpFunctionalExtensions;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.MediaProcessing;
using FileService.VideoProcessing.FfmpegProcess;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline.StepHandlers;

public sealed class GenerateHlsStepHandler : IProcessingStepHandler
{
    private readonly ILogger<GenerateHlsStepHandler> _logger;
    private readonly IFfmpegProcessRunner _ffmpegProcessRunner;
    private readonly IFileStorageProvider _fileStorageProvider;

    public GenerateHlsStepHandler(
        ILogger<GenerateHlsStepHandler> logger,
        IFfmpegProcessRunner ffmpegProcessRunner,
        IFileStorageProvider fileStorageProvider)
    {
        _logger = logger;
        _ffmpegProcessRunner = ffmpegProcessRunner;
        _fileStorageProvider = fileStorageProvider;
    }

    public string StepName => StepNames.GenerateHls;

    public async Task<Result<ProcessingContext, Error>> ExecuteAsync(ProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating HLS for video asset {VideoAssetId}",
            context.VideoProcess.VideoAssetId);

        string inputFileUrl;

        if (!string.IsNullOrEmpty(context.MediaAssetUrl))
        {
            inputFileUrl = context.MediaAssetUrl;
        }
        else
        {
            _logger.LogDebug("Input media URL is missing from processing context; generating a new presigned URL");

            var inputFileUrlResult = await _fileStorageProvider
                .GenerateDownloadUrlAsync(context.VideoAsset.UploadKey, cancellationToken);
            if (inputFileUrlResult.IsFailure)
                return inputFileUrlResult.Error;

            inputFileUrl = inputFileUrlResult.Value;
        }

        if (string.IsNullOrEmpty(context.HlsOutputDirectory))
        {
            return FileErrors.HlsProcessingFailed();
        }

        if (context.VideoProcess.MetaData is null)
            return FileErrors.HlsProcessingFailed("Video metadata is required for HLS generation");

        var result = await _ffmpegProcessRunner.GenerateHlsAsync(
            inputFileUrl,
            context.HlsOutputDirectory,
            context.VideoProcess.MetaData,
            cancellationToken);
        if (result.IsFailure)
            return result.Error;

        return context;
    }
}
