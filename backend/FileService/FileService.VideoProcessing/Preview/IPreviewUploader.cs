using CSharpFunctionalExtensions;
using FileService.Domain;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Preview;

public interface IPreviewUploader
{
    Task<Result<(List<StorageKey> PreviewKeys, StorageKey? SpriteKey), Error>> GenerateAndUploadPreviewsAsync(
        string inputFileUrl,
        string workingDirectory,
        Guid videoAssetId,
        List<TimeSpan> timestamps,
        CancellationToken cancellationToken);
}