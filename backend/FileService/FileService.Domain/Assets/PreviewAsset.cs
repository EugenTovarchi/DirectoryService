using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain.Assets;

public class PreviewAsset : MediaAsset
{
    public const long MAX_SIZE = 10_485_760;

    public const string ASSET_TYPE = "preview";
    public const string LOCATION = "file-service-preview";
    public const string RAW_PREFIX = "raw";
    public const string ALLOWED_CONTENT_TYPE = "image";

    public static readonly string[] AllowedExtensions = ["jpeg", "jpg", "png", "webp"];

    private PreviewAsset() { }

    private PreviewAsset(
        Guid id,
        MediaData mediaData,
        MediaStatus mediaStatus,
        Guid ownerId,
        string ownerType,
        StorageKey key)
        : base(id, mediaData, mediaStatus, AssetType.PREVIEW, ownerId, ownerType, key, isDirectUpload: true)
    {
    }

    public static UnitResult<Error> Validate(MediaData mediaData)
    {
        if (!AllowedExtensions.Contains(mediaData.FileName.Extension, StringComparer.Ordinal))
        {
            return Error.Validation("preview.invalid.extension",
                $"File extension must be one of :{string.Join(",", AllowedExtensions)}");
        }

        if (mediaData.ContentType.MediaType != MediaType.IMAGE)
        {
            return Error.Validation("preview.invalid.content-type",
                $"File content type must be {ALLOWED_CONTENT_TYPE}");
        }

        if (mediaData.Size > MAX_SIZE)
        {
            return Error.Validation("preview.invalid.max-size",
                $"File size must be less than: {MAX_SIZE} bytes");
        }

        return UnitResult.Success<Error>();
    }

    public static Result<PreviewAsset, Error> CreateForUpload(
        Guid id,
        MediaData mediaData,
        Guid ownerId,
        string ownerType)
    {
        UnitResult<Error> validationResult = Validate(mediaData);
        if (validationResult.IsFailure)
            return validationResult.Error;

        if (ownerId == Guid.Empty)
        {
            return Error.Validation("video.invalid.owner-id", "OwnerId cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(ownerType))
        {
            return Error.Validation("video.invalid.owner-type", "OwnerType cannot be empty");
        }

        var key = StorageKey.Create(id.ToString(), prefix: null, LOCATION);
        if (key.IsFailure)
            return key.Error;

        return new PreviewAsset(id, mediaData, MediaStatus.UPLOADING, ownerId, ownerType, key.Value);
    }
}