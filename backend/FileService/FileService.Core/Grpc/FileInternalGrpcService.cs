using FileService.Contracts.Grpc;
using FileService.Contracts.Requests;
using FileService.Core.Features;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using SharedService.SharedKernel;

namespace FileService.Core.Grpc;

/// <summary>
/// Internal gRPC API: точечные синхронные запросы от других backend-сервисов к FileService.
/// </summary>
public sealed class FileInternalGrpcService : FileInternal.FileInternalBase
{
    private readonly GetMediaAssetInfoHandler _getMediaAssetInfoHandler;
    private readonly GetMediaAssetsInfoHandler _getMediaAssetsInfoHandler;
    private readonly GetVideoInfoHandler _getVideoInfoHandler;
    private readonly CheckMediaAssetExistHandler _checkMediaAssetExistHandler;

    public FileInternalGrpcService(
        GetMediaAssetInfoHandler getMediaAssetInfoHandler,
        GetMediaAssetsInfoHandler getMediaAssetsInfoHandler,
        CheckMediaAssetExistHandler checkMediaAssetExistHandler,
        GetVideoInfoHandler getVideoInfoInfoHandler)
    {
        _getMediaAssetInfoHandler = getMediaAssetInfoHandler;
        _getMediaAssetsInfoHandler = getMediaAssetsInfoHandler;
        _checkMediaAssetExistHandler = checkMediaAssetExistHandler;
        _getVideoInfoHandler = getVideoInfoInfoHandler;
    }

    public override async Task<GetMediaAssetInfoReply> GetMediaAssetInfo(
        GetMediaAssetInfoRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.MediaAssetId, out Guid mediaAssetId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid media asset id"));
        }

        var result = await _getMediaAssetInfoHandler.Handle(mediaAssetId, context.CancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Media asset not found"));
        }

        var mediaAsset = result.Value;

        var reply = new GetMediaAssetInfoReply
        {
            Id = mediaAsset.Id.ToString(),
            Status = mediaAsset.Status,
            AssetType = mediaAsset.AssetType,
            CreatedAt = mediaAsset.CreatedAt.ToString("O"),
            UpdatedAt = mediaAsset.UpdatedAt.ToString("O")
        };

        if (mediaAsset.ViewUrl is not null)
            reply.ViewUrl = mediaAsset.ViewUrl;

        if (mediaAsset.DownloadUrl is not null)
            reply.DownloadUrl = mediaAsset.DownloadUrl;

        if (mediaAsset.ThumbnailUrl is not null)
            reply.ThumbnailUrl = mediaAsset.ThumbnailUrl;

        if (mediaAsset.Size.HasValue)
            reply.Size = mediaAsset.Size.Value;

        if (mediaAsset.FileName is not null)
            reply.FileName = mediaAsset.FileName;

        if (mediaAsset.ContentType is not null)
            reply.ContentType = mediaAsset.ContentType;

        return reply;
    }

    public override async Task<GetMediaAssetsInfoReply> GetMediaAssetsInfo(
        GetMediaAssetsInfoRequest request,
        ServerCallContext context)
    {
        var mediaAssetIds = new List<Guid>();

        foreach (string mediaAssetId in request.MediaAssetIds)
        {
            if (!Guid.TryParse(mediaAssetId, out Guid parsedMediaAssetId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid media asset id"));
            }

            mediaAssetIds.Add(parsedMediaAssetId);
        }

        var result = await _getMediaAssetsInfoHandler
            .Handle(new GetMediaAssetsRequest(mediaAssetIds), context.CancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new RpcException(new Status(StatusCode.Internal, "Failed to get media assets info"));
        }

        var reply = new GetMediaAssetsInfoReply();

        reply.MediaAssets.AddRange(result.Value.MediaAssets.Select(mediaAsset =>
        {
            var item = new MediaAssetInfoItem
            {
                Id = mediaAsset.Id.ToString(),
                Status = mediaAsset.Status,
                AssetType = mediaAsset.AssetType
            };

            if (mediaAsset.ViewUrl is not null)
                item.ViewUrl = mediaAsset.ViewUrl;

            if (mediaAsset.DownloadUrl is not null)
                item.DownloadUrl = mediaAsset.DownloadUrl;

            if (mediaAsset.ThumbnailUrl is not null)
                item.ThumbnailUrl = mediaAsset.ThumbnailUrl;

            return item;
        }));

        return reply;
    }

    public override async Task<CheckMediaAssetExistsReply> CheckMediaAssetExists(
        CheckMediaAssetExistsRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.MediaAssetId, out Guid mediaAssetId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid media asset id"));
        }

        var result = await _checkMediaAssetExistHandler.Handle(mediaAssetId, context.CancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new RpcException(new Status(StatusCode.Internal, "Failed to check media asset"));
        }

        return new CheckMediaAssetExistsReply { IsExist = result.Value.IsExist };
    }

    public override async Task<GetVideoInfoReply> GetVideoInfo(
        GetVideoInfoRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.MediaAssetId, out Guid mediaAssetId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid media asset id"));
        }

        var result = await _getVideoInfoHandler.Handle(mediaAssetId, context.CancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw ToRpcException(result.Error);
        }

        var video = result.Value;

        var reply = new GetVideoInfoReply
        {
            Id = video.Id.ToString(),
            FileName = video.FileName,
            ContentType = video.ContentType,
            Status = video.Status,
            CreatedAt = Timestamp.FromDateTime(video.CreatedAt),
            UpdatedAt = Timestamp.FromDateTime(video.UpdatedAt),
            Size = video.Size,
        };

        if (video.Duration.HasValue)
        {
            reply.Duration =
                Duration.FromTimeSpan(video.Duration.Value);
        }

        if (video.Width.HasValue)
        {
            reply.Width = video.Width.Value;
        }

        if (video.Height.HasValue)
        {
            reply.Height = video.Height.Value;
        }

        if (video.HasAudio.HasValue)
        {
            reply.HasAudio = video.HasAudio.Value;
        }

        return reply;
    }

    /// <summary>
    /// Преобразует первую прикладную ошибку FileService в стандартную ошибку gRPC.
    /// Клиент получит выбранный <see cref="StatusCode"/> и текст ошибки в <see cref="Status.Detail"/>.
    /// </summary>
    /// <remarks>
    /// Validation означает некорректный запрос, NotFound — отсутствие ресурса,
    /// Conflict — невозможность выполнить операцию в текущем состоянии.
    /// Остальные и пустые наборы ошибок скрываются за Internal.
    /// </remarks>
    private static RpcException ToRpcException(Failure failure)
    {
        Error error = failure.FirstOrDefault()
                      ?? Error.Failure("server.internal", "Failed to get video info");

        StatusCode statusCode = error.Type switch
        {
            ErrorType.VALIDATION => StatusCode.InvalidArgument,
            ErrorType.NOT_FOUND => StatusCode.NotFound,
            ErrorType.CONFLICT => StatusCode.FailedPrecondition,
            _ => StatusCode.Internal
        };

        return new RpcException(new Status(statusCode, error.Message));
    }
}
