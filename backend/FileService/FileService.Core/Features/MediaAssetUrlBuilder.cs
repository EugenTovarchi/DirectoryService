using CSharpFunctionalExtensions;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using SharedService.SharedKernel;

namespace FileService.Core.Features;

internal static class MediaAssetUrlBuilder
{
    public static bool CanExposeMediaInfo(MediaAsset mediaAsset)
    {
        return mediaAsset.Status is MediaStatus.UPLOADED or MediaStatus.PROCESSING or MediaStatus.READY;
    }

    public static bool CanExposeDownloadUrl(MediaAsset mediaAsset)
    {
        return mediaAsset.Status is MediaStatus.UPLOADED or MediaStatus.PROCESSING or MediaStatus.READY;
    }

    public static async Task<Result<MediaAssetUrls, Error>> BuildAsync(
        MediaAsset mediaAsset,
        IFileStorageProvider fileStorageProvider,
        CancellationToken cancellationToken)
    {
        string? viewUrl = null;
        string? downloadUrl = null;

        if (CanExposeDownloadUrl(mediaAsset))
        {
            var downloadUrlResult = await fileStorageProvider
                .GenerateDownloadUrlAsync(mediaAsset.UploadKey, cancellationToken);
            if (downloadUrlResult.IsFailure)
                return downloadUrlResult.Error;

            downloadUrl = downloadUrlResult.Value;
        }

        StorageKey? viewKey = GetViewKey(mediaAsset);
        if (viewKey is not null)
        {
            var viewUrlResult = await fileStorageProvider.GenerateDownloadUrlAsync(viewKey, cancellationToken);
            if (viewUrlResult.IsFailure)
                return viewUrlResult.Error;

            viewUrl = viewUrlResult.Value;
        }

        return new MediaAssetUrls(viewUrl, downloadUrl, ThumbnailUrl: null);
    }

    private static StorageKey? GetViewKey(MediaAsset mediaAsset)
    {
        return mediaAsset switch
        {
            VideoAsset { Status: MediaStatus.READY, Key: not null } => mediaAsset.Key,
            PhotoAsset { Status: MediaStatus.READY, Key: not null } => mediaAsset.Key,
            PreviewAsset { Status: MediaStatus.READY, Key: not null } => mediaAsset.Key,
            _ => null
        };
    }
}

internal sealed record MediaAssetUrls(string? ViewUrl, string? DownloadUrl, string? ThumbnailUrl);
