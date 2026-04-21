using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain.Assets;

public class PhotoAsset : MediaAsset
{
    public const long MAX_SIZE = 5_242_880; // 5MB
    public const string LOCATION = "photos";
    public const string ALLOWED_CONTENT_TYPE = "image";
    public static readonly string[] AllowedExtensions = ["jpeg", "jpg", "png"];

    private PhotoAsset() { }

    private PhotoAsset(
        Guid id,
        MediaData mediaData,
        MediaStatus mediaStatus,
        Guid ownerId,
        string ownerType,
        StorageKey key)
        : base(id, mediaData, mediaStatus, AssetType.PHOTO, ownerId, ownerType, key, true)
    {
    }

    public static UnitResult<Error> Validate(MediaData mediaData)
    {
        if (!AllowedExtensions.Contains(mediaData.FileName.Extension))
        {
            return Error.Validation("photo.invalid.extension",
                $"File extension must be one of :{string.Join(",", AllowedExtensions)}");
        }

        if (mediaData.ContentType.MediaType != MediaType.IMAGE)
        {
            return Error.Validation("photo.invalid.content-type",
                $"File content type must be {ALLOWED_CONTENT_TYPE}");
        }

        if (mediaData.Size > MAX_SIZE)
        {
            return Error.Validation("photo.invalid.max-size",
                $"File size must be less than: {MAX_SIZE} bytes");
        }

        return UnitResult.Success<Error>();
    }

    public static Result<PhotoAsset, Error> CreateForUpload(
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

        var key = StorageKey.Create($"photo_{id}", null, LOCATION);
        if (key.IsFailure)
            return key.Error;

        return new PhotoAsset(id, mediaData, MediaStatus.UPLOADING, ownerId, ownerType, key.Value);
    }
}