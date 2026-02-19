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

    public UnitResult<Error> MarkUpload(MediaAsset media)
    {
        var switchResult = MediaStatusExtensions.SwitchStatus(media.Status, MediaStatus.UPLOADED);
        if (switchResult.IsFailure)
            return switchResult.Error;

        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }
}