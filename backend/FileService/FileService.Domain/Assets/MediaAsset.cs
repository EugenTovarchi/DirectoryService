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

    public Guid OwnerId { get; protected set; }

    public string OwnerType { get; protected set; } = string.Empty;

    public DateTime CreatedAt { get; protected set; }

    public DateTime UpdatedAt { get; protected set; }

    public StorageKey? Key { get; protected set; }

    public StorageKey? RawKey { get; protected set; }

    public MediaStatus Status { get; protected set; }

    public StorageKey UploadKey => RequiresProcessing() ? RawKey! : Key!;

    protected MediaAsset() { }

    protected MediaAsset(
        Guid id,
        MediaData mediaData,
        MediaStatus status,
        AssetType assetType,
        Guid ownerId,
        string ownerType,
        StorageKey key,
        bool isDirectUpload = false)
    {
        Id = id;
        MediaData = mediaData;
        Status = status;
        AssetType = assetType;
        OwnerId = ownerId;
        OwnerType = ownerType;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        AssetType = assetType;
        if (isDirectUpload)
            Key = key;
        else
            RawKey = key;
    }

    public static Result<MediaAsset, Error> CreateForUpload(
        MediaData mediaData,
        AssetType assetType,
        Guid ownerId,
        string ownerType)
    {
        var assetId = Guid.NewGuid();

        switch (assetType)
        {
            case AssetType.VIDEO:
                var videoResult = VideoAsset.CreateForUpload(assetId, mediaData, ownerId, ownerType);
                return videoResult.IsFailure ? videoResult.Error : videoResult.Value;

            case AssetType.PREVIEW:
                var previewResult = PreviewAsset.CreateForUpload(assetId, mediaData, ownerId, ownerType);
                return previewResult.IsFailure ? previewResult.Error : previewResult.Value;

            case AssetType.PHOTO:
                var avatarResult = PhotoAsset.CreateForUpload(assetId, mediaData, ownerId, ownerType);
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

    public virtual bool RequiresProcessing() => false;
}