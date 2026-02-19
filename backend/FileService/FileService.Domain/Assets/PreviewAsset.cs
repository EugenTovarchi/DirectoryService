using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain.Assets;

public class PreviewAsset : MediaAsset
{
    public const long MAX_SIZE = 10_485_760;

    public const string ASSET_TYPE = "preview";
    public const string LOCATION = "preview";
    public const string RAW_PREFIX = "raw";
    public const string ALLOWED_CONTENT_TYPE = "image";

    public static readonly string[] AllowedExtensions = ["jpeg", "jpg", "png", "webp"];

    private PreviewAsset() { }

    private PreviewAsset(
        Guid id,
        MediaData mediaData,
        MediaStatus mediaStatus,
        StorageKey key)
        : base(id, mediaData, mediaStatus, AssetType.PREVIEW, key)
    {
    }

    public static UnitResult<Error> Validate(MediaData mediaData)
    {
        if (!AllowedExtensions.Contains(mediaData.FileName.Extension))
        {
            return Error.Validation("preview.invalid.extension",
                $"File extension must be one of :{string.Join(",", AllowedExtensions)}");
        }

        if (mediaData.ContentType.MediaType != MediaType.VIDEO)
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

    public static Result<PreviewAsset, Error> CreateForUpload(Guid id, MediaData mediaData)
    {
        UnitResult<Error> validationResult = Validate(mediaData);
        if (validationResult.IsFailure)
            return validationResult.Error;

        var key = StorageKey.Create(id.ToString(),  null, LOCATION);
        if (key.IsFailure)
            return key.Error;

        return new PreviewAsset(id, mediaData, MediaStatus.UPLOADING, key.Value);
    }
}