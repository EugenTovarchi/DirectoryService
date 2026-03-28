using CSharpFunctionalExtensions;
using FileService.Core.FilesStorage;
using FileService.Domain.MediaProcessing;
using FileService.VideoProcessing.FfmpegProcess;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline.StepHandlers;

public sealed class ExtractMetadataStepHandler : IProcessingStepHandler
{
    private readonly ILogger<ExtractMetadataStepHandler> _logger;
    private readonly IFfmpegProcessRunner _ffmpegProcessRunner;
    private readonly IFileStorageProvider _fileStorageProvider;

    public ExtractMetadataStepHandler(
        ILogger<ExtractMetadataStepHandler> logger,
        IFfmpegProcessRunner ffmpegProcessRunner,
        IFileStorageProvider fileStorageProvider)
    {
        _logger = logger;
        _ffmpegProcessRunner = ffmpegProcessRunner;
        _fileStorageProvider = fileStorageProvider;
    }

    public string StepName => StepNames.ExtractMetadata;

    public async Task<Result<ProcessingContext, Error>> ExecuteAsync(ProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting metadata for video asset: {VideoAsset}", context.VideoProcess.VideoAssetId);

        Result<string, Error> inputFileUrl = await _fileStorageProvider
            .GenerateDownloadUrlAsync(context.VideoAsset.UploadKey, cancellationToken);
        if(inputFileUrl.IsFailure)
            return inputFileUrl.Error;

        context.SetMediaAssetUrl(inputFileUrl.Value);

        var metadataResult = await _ffmpegProcessRunner.ExtractMetadataAsync(inputFileUrl.Value, cancellationToken);
        if (metadataResult.IsFailure)
            return metadataResult.Error;

        context.VideoProcess.SetMetadata(metadataResult.Value);

        return context;
    }
}