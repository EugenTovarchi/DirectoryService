using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain.Assets;

/// <summary>
/// Базовая логика для всех типов медиа-файлов.
/// </summary>
public abstract class MediaAsset
{
    public Guid Id { get; protected set; }
    public MediaData MediaData { get; protected set; } = null!;
    public AssetType AssetType { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime UpdatedAt { get; protected set; }
    public StorageKey Key { get; protected set; } = null!;

    // public MediaOwner Owner { get; protected set; } = null!;
    public MediaStatus Status { get; protected set; }

    protected MediaAsset() { }

    protected MediaAsset(
        Guid id,
        MediaData mediaData,
        MediaStatus status,
        AssetType assetType,
        StorageKey key)
    {
        Id = id;
        MediaData = mediaData;
        Status = status;
        AssetType = assetType;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        AssetType = assetType;
        Key = key;
    }

    public static Result<MediaAsset, Error> CreateForUpload(MediaData mediaData, AssetType assetType)
    {
        var assetId = Guid.NewGuid();

        switch (assetType)
        {
            case AssetType.VIDEO:
                var videoResult = VideoAsset.CreateForUpload(assetId, mediaData);
                return videoResult.IsFailure ? videoResult.Error : videoResult.Value;

            case AssetType.PREVIEW:
                var previewResult = VideoAsset.CreateForUpload(assetId, mediaData);
                return previewResult.IsFailure ? previewResult.Error : previewResult.Value;

            case AssetType.AVATAR:
                var avatarResult = VideoAsset.CreateForUpload(assetId, mediaData);
                return avatarResult.IsFailure ? avatarResult.Error : avatarResult.Value;

            default:
                throw new ArgumentOutOfRangeException(nameof(assetType), assetType, null);
        }
    }

    public UnitResult<Error> MarkUploaded()
    {
        var switchResult = MediaStatusExtensions.SwitchStatus(Status, MediaStatus.UPLOADED);
        if (switchResult.IsFailure)
            return switchResult.Error;

        Status = MediaStatus.UPLOADED;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    public UnitResult<Error> MarkFailed()
    {
        var switchResult = MediaStatusExtensions.SwitchStatus(Status, MediaStatus.FAILED);
        if (switchResult.IsFailure)
            return switchResult.Error;

        Status = MediaStatus.FAILED;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    public UnitResult<Error> MarkReady()
    {
        var switchResult = MediaStatusExtensions.SwitchStatus(Status, MediaStatus.READY);
        if (switchResult.IsFailure)
            return switchResult.Error;

        Status = MediaStatus.READY;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    public UnitResult<Error> MarkDeleted()
    {
        var switchResult = MediaStatusExtensions.SwitchStatus(Status, MediaStatus.DELETED);
        if (switchResult.IsFailure)
            return switchResult.Error;

        Status = MediaStatus.DELETED;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    public UnitResult<Error> MarkUploading()
    {
        Status = MediaStatus.UPLOADING;

        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }
}