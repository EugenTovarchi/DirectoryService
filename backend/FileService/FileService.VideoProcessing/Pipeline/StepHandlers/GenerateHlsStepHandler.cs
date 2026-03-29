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
    private readonly ILogger<ExtractMetadataStepHandler> _logger;
    private readonly IFfmpegProcessRunner _ffmpegProcessRunner;
    private readonly IFileStorageProvider _fileStorageProvider;

    public GenerateHlsStepHandler(
        ILogger<ExtractMetadataStepHandler> logger,
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
        _logger.LogInformation("Generate hls  for video asset: {VideoAsset}",
            context.VideoProcess.VideoAssetId);

        string inputFileUrl;

        if (!string.IsNullOrEmpty(context.MediaAssetUrl))
        {
            inputFileUrl = context.MediaAssetUrl;
        }
        else
        {
            _logger.LogDebug("InputFileUrl not caught, generating new presigned url");

            var inputFileUrlResult = await _fileStorageProvider
                .GenerateDownloadUrlAsync(context.VideoAsset.UploadKey, cancellationToken);
            if(inputFileUrlResult.IsFailure)
                return inputFileUrlResult.Error;

            inputFileUrl = inputFileUrlResult.Value;

            _logger.LogInformation("Extracting metadata for video asset: {VideoAsset}", context.VideoProcess.VideoAssetId);
        }

        if (string.IsNullOrEmpty(context.HlsOutputDirectory))
        {
            return FileErrors.HlsProcessingFailed();
        }

        if (context.VideoProcess.MetaData is null)
        {
            _logger.LogWarning("Metadata is null, progress tracking will be disabled");
        }

        var result = await _ffmpegProcessRunner.GenerateHlsAsync(inputFileUrl, context.HlsOutputDirectory, cancellationToken);
        if(result.IsFailure)
            return result.Error;

        return context;
    }
}