using CSharpFunctionalExtensions;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.MediaProcessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline.StepHandlers;

public sealed class UploadHlsStepHandler : IProcessingStepHandler
{
    private readonly ILogger<UploadHlsStepHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly VideoProcessingOptions _options;

    public UploadHlsStepHandler(
        ILogger<UploadHlsStepHandler> logger,
        IOptions<VideoProcessingOptions> options,
        IFileStorageProvider fileStorageProvider)
    {
        _options = options.Value;
        _logger = logger;
        _fileStorageProvider = fileStorageProvider;
    }

    public string StepName => StepNames.UploadHls;

    public async Task<Result<ProcessingContext, Error>> ExecuteAsync(ProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upload hls to S3 for video asset: {VideoAsset}", context.VideoProcess.VideoAssetId);

        if (string.IsNullOrWhiteSpace(context.HlsOutputDirectory))
            return FileErrors.HlsProcessingFailed("HLS output directory isn`t set");

        if (!Directory.Exists(context.HlsOutputDirectory))
            return FileErrors.HlsProcessingFailed("HLS output directory does`t exist");

        string[] hlsFiles = Directory.GetFiles(context.HlsOutputDirectory, "*.*", SearchOption.AllDirectories);
        if (hlsFiles.Length == 0)
            return FileErrors.HlsProcessingFailed("HLS output directory is empty");

        var hlsRootKey = context.VideoAsset.GetHlsRootKey();
        if (hlsRootKey.IsFailure)
            return hlsRootKey.Error;

        using var throttler = new SemaphoreSlim(_options.UploadDegreeOfParallelism);
        Task<UnitResult<Error>>[] uploadTasks = hlsFiles.Select(async file =>
        {
            await throttler.WaitAsync(cancellationToken);
            try
            {
                return await UploadHlsFileAsync(hlsRootKey.Value, file, cancellationToken);
            }
            finally
            {
                throttler.Release();
            }
        }).ToArray();

        UnitResult<Error>[] results = await Task.WhenAll(uploadTasks);

        var firstError = results.FirstOrDefault(e => e.IsFailure);
        if (firstError.IsFailure)
            return firstError.Error;

        _logger.LogInformation("Successfully uploaded {fileCount} hls files for video asset: {VideoAsset}",
            hlsFiles.Length, context.VideoProcess.VideoAssetId);

        var masterPlaylistKey = context.VideoAsset.GetMasterPlaylistKey();
        if (masterPlaylistKey.IsFailure)
            return masterPlaylistKey.Error;

        var setMasterKeyResult = context.VideoAsset.GetHlsMasterPlaylistKey(masterPlaylistKey.Value);
        if (setMasterKeyResult.IsFailure)
            return setMasterKeyResult.Error;

        return context;
    }

    private async Task<UnitResult<Error>> UploadHlsFileAsync(
        StorageKey hlsRootKey,
        string localFilePath,
        CancellationToken cancellationToken)
    {
        string fileName = Path.GetFileName(localFilePath);

        Result<StorageKey, Error> newStorageKeyResult = hlsRootKey.AppendKey(fileName);
        if (newStorageKeyResult.IsFailure)
            return newStorageKeyResult;

        string contentType = GetContentType(localFilePath);

        await using FileStream fileStream = File.OpenRead(localFilePath);

        return await _fileStorageProvider.UploadFileAsync(
            newStorageKeyResult.Value,
            fileStream,
            contentType,
            cancellationToken);
    }

    private string GetContentType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".m3u8" => "application/vnd.apple.mpegurl",
            ".ts" => "video/mp2t",
            _ => "application/octet-stream"
        };
    }
}