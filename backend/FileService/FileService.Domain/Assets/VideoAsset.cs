using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain.Assets;

public class VideoAsset : MediaAsset
{
    public const long MAX_SIZE = 5_368_709_120;

    public const string LOCATION = "file-service-videos";
    public const string RAW_PREFIX = "raw";
    public const string HLS_PREFIX = "hls";
    public const string ALLOWED_CONTENT_TYPE = "video";

    public const string MASTER_PLAYLIST_NAME = "master.m3u8";
    public const string STREAM_PLAYLIST_PATTERN = "%v_stream.m3u8";
    public const string SEGMENT_FILE_PATTERN = "%v_%06d.ts";

    public static readonly string[] AllowedExtensions = ["mp4", "mkv", "avi", "mov"];

    private VideoAsset(
        Guid id,
        MediaData mediaData,
        MediaStatus mediaStatus,
        StorageKey rawKey)
    : base(id, mediaData, mediaStatus, AssetType.VIDEO, rawKey)
    {
    }

    private VideoAsset() { }

    public static UnitResult<Error> Validate(MediaData mediaData)
    {
        if (!AllowedExtensions.Contains(mediaData.FileName.Extension))
        {
            return Error.Validation("video.invalid.extension",
                $"File extension must be one of :{string.Join(",", AllowedExtensions)}");
        }

        if (mediaData.ContentType.MediaType != MediaType.VIDEO)
        {
            return Error.Validation("video.invalid.content-type",
                $"File content type must be {ALLOWED_CONTENT_TYPE}");
        }

        if (mediaData.Size > MAX_SIZE)
        {
            return Error.Validation("video.invalid.max-size",
                $"File size must be less than: {MAX_SIZE} bytes");
        }

        return UnitResult.Success<Error>();
    }

    public static Result<VideoAsset, Error> CreateForUpload(Guid id, MediaData mediaData)
    {
        UnitResult<Error> validationResult = Validate(mediaData);
        if (validationResult.IsFailure)
            return validationResult.Error;

        Result<StorageKey, Error> rawKey = StorageKey.Create(id.ToString(),  RAW_PREFIX, LOCATION);
        if (rawKey.IsFailure)
            return rawKey.Error;

        return new VideoAsset(id, mediaData, MediaStatus.UPLOADING, rawKey.Value);
    }

    public override bool RequiresProcessing() => true;

    public UnitResult<Error> StartProcessing()
    {
        if (Status != MediaStatus.UPLOADED)
            return Error.Validation("asset.invalid.status", "Can started only from UPLOADED status");

        if (!RequiresProcessing())
            return Error.Validation("not.required.processing", "This asset type does not require processing");

        Status = MediaStatus.PROCESSING;
        UpdatedAt = DateTime.UtcNow;
        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Путь будет выглядеть след образом:
    /// videos/hls/videoid/master.m3u8
    /// videos/hls/videoid/file1.ts
    /// videos/hls/videoid/file2.ts.
    /// </summary>
    /// <returns>Создаем storageKey с папкой: "hls".</returns>
    public Result<StorageKey, Error> GetHlsRootKey()
    {
        return StorageKey.Create(Id.ToString(), HLS_PREFIX, LOCATION);
    }

    public Result<StorageKey, Error> GetMasterPlaylistKey()
    {
        Result<StorageKey, Error> hlsRoot = GetHlsRootKey();
        if(hlsRoot.IsFailure)
            return hlsRoot.Error;

        return hlsRoot.Value.AppendKey(MASTER_PLAYLIST_NAME);
    }

    public UnitResult<Error> GetHlsMasterPlaylistKey(StorageKey key)
    {
        if (Status != MediaStatus.PROCESSING)
            return Error.Validation("video.status.invalid", "Can only set processed data during processing");

        Key = key;
        UpdatedAt = DateTime.UtcNow;

        return UnitResult.Success<Error>();
    }

    public UnitResult<Error> CompleteProcessing()
    {
        if (Status != MediaStatus.PROCESSING)
        {
            return Error.Validation("asset.invalid.status.transition",
                "Can only complete processing from PROCESSING status");
        }

        Status = MediaStatus.READY;
        UpdatedAt = DateTime.UtcNow;
        return UnitResult.Success<Error>();
    }
}