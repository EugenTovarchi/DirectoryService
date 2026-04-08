using CSharpFunctionalExtensions;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.VideoProcessing.FfmpegProcess;
using FileService.VideoProcessing.Pipeline.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Preview;

public class PreviewUploader : IPreviewUploader
{
    private readonly IFfmpegProcessRunner _ffmpegRunner;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly PreviewOptions _previewOptions;
    private readonly ILogger<PreviewUploader> _logger;

    public PreviewUploader(
        IFfmpegProcessRunner ffmpegRunner,
        IFileStorageProvider fileStorageProvider,
        IOptions<PreviewOptions> previewOptions,
        ILogger<PreviewUploader> logger)
    {
        _ffmpegRunner = ffmpegRunner;
        _fileStorageProvider = fileStorageProvider;
        _previewOptions = previewOptions.Value;
        _logger = logger;
    }

    public async Task<Result<(List<StorageKey> PreviewKeys, StorageKey? SpriteKey), Error>>
        GenerateAndUploadPreviewsAsync(
            string inputFileUrl,
            string workingDirectory,
            Guid videoAssetId,
            List<TimeSpan> timestamps,
            CancellationToken cancellationToken)
    {
        // Создаем папку для превью
        string previewDirectory = Path.Combine(workingDirectory, "previews");
        Directory.CreateDirectory(previewDirectory);

        // Извлекаем кадры
        var previewPaths = await ExtractFramesAsync(
            inputFileUrl,
            previewDirectory,
            timestamps,
            cancellationToken);

        if (previewPaths.IsFailure)
            return previewPaths.Error;

        // Создаем спрайт-лист
        string? spriteSheetPath = null;
        if (previewPaths.Value.Count > 1)
        {
            spriteSheetPath = await CreateSpriteSheetAsync(
                previewPaths.Value,
                previewDirectory,
                cancellationToken);
        }

        // Загружаем все превьюшки
        var previewKeys = await UploadPreviewsAsync(
            previewPaths.Value,
            videoAssetId,
            cancellationToken);

        if (previewKeys.IsFailure)
            return previewKeys.Error;

        // Загружаем спрайт-лист
        StorageKey? spriteKey = null;
        if (!string.IsNullOrEmpty(spriteSheetPath) && File.Exists(spriteSheetPath))
        {
            spriteKey = await UploadSpriteSheetAsync(
                spriteSheetPath,
                videoAssetId,
                cancellationToken);
        }

        return (previewKeys.Value, spriteKey);
    }

    private async Task<Result<List<string>, Error>> ExtractFramesAsync(
        string inputFileUrl,
        string previewDirectory,
        List<TimeSpan> timestamps,
        CancellationToken cancellationToken)
    {
        var previewPaths = new List<string>();

        for (int i = 0; i < timestamps.Count; i++)
        {
            string fileName = _previewOptions.FileNamePattern.Replace("{index}", i.ToString());
            string outputPath = Path.Combine(previewDirectory, fileName);

            var result = await _ffmpegRunner.ExtractFrameAsync(
                inputFileUrl,
                outputPath,
                timestamps[i],
                cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogError("Failed to extract frame at {Timestamp}s: {Error}",
                    timestamps[i].TotalSeconds, result.Error);
                return result.Error;
            }

            previewPaths.Add(outputPath);
        }

        return previewPaths;
    }

    private async Task<string?> CreateSpriteSheetAsync(
        List<string> previewPaths,
        string previewDirectory,
        CancellationToken cancellationToken)
    {
        string spriteSheetPath = Path.Combine(previewDirectory, _previewOptions.SpriteSheetFileName);

        var spriteResult = await _ffmpegRunner.CreateSpriteSheetAsync(
            previewPaths,
            spriteSheetPath,
            cancellationToken);

        if (spriteResult.IsFailure)
        {
            _logger.LogWarning("Failed to create sprite sheet: {Error}", spriteResult.Error);
            return null;
        }

        return spriteSheetPath;
    }

    private async Task<Result<List<StorageKey>, Error>> UploadPreviewsAsync(
        List<string> previewPaths,
        Guid videoAssetId,
        CancellationToken cancellationToken)
    {
        var previewKeys = new List<StorageKey>();

        foreach (string previewPath in previewPaths)
        {
            string fileName = Path.GetFileName(previewPath);

            var storageKeyResult = CreateStorageKey(videoAssetId, fileName);
            if (storageKeyResult.IsFailure)
                return storageKeyResult.Error;

            var uploadResult = await UploadFileAsync(
                previewPath,
                storageKeyResult.Value,
                cancellationToken);

            if (uploadResult.IsFailure)
                return uploadResult.Error;

            previewKeys.Add(uploadResult.Value);

            _logger.LogDebug("Uploaded preview {FileName} to: {FullPath}",
                fileName, uploadResult.Value.FullPath);
        }

        return previewKeys;
    }

    private async Task<StorageKey?> UploadSpriteSheetAsync(
        string spriteSheetPath,
        Guid videoAssetId,
        CancellationToken cancellationToken)
    {
        string spriteFileName = _previewOptions.SpriteSheetFileName;

        var storageKeyResult = CreateStorageKey(videoAssetId, spriteFileName);
        if (storageKeyResult.IsFailure)
        {
            _logger.LogWarning("Failed to create storage key for sprite sheet: {Error}",
                storageKeyResult.Error);
            return null;
        }

        var uploadResult = await UploadFileAsync(
            spriteSheetPath,
            storageKeyResult.Value,
            cancellationToken);

        if (uploadResult.IsFailure)
        {
            _logger.LogWarning("Failed to upload sprite sheet: {Error}", uploadResult.Error);
            return null;
        }

        _logger.LogDebug("Uploaded sprite sheet to: {FullPath}", uploadResult.Value.FullPath);
        return uploadResult.Value;
    }

    private Result<StorageKey, Error> CreateStorageKey(Guid videoAssetId, string fileName)
    {
        return StorageKey.Create(
            fileName,
            videoAssetId.ToString(),
            PreviewAsset.LOCATION);
    }

    private async Task<Result<StorageKey, Error>> UploadFileAsync(
        string filePath,
        StorageKey storageKey,
        CancellationToken cancellationToken)
    {
        var contentTypeResult = ContentType.Create("image/jpeg");
        if (contentTypeResult.IsFailure)
            return contentTypeResult.Error;

        await using var fileStream = File.OpenRead(filePath);
        var uploadResult = await _fileStorageProvider.UploadFileAsync(
            storageKey,
            fileStream,
            contentTypeResult.Value.Value,
            cancellationToken);

        if (uploadResult.IsFailure)
            return uploadResult.Error;

        return storageKey;
    }
}