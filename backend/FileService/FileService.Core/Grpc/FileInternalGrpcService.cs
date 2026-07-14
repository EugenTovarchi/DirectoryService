using FileService.Contracts.Grpc;
using FileService.Contracts.Requests;
using FileService.Core.Features;
using Grpc.Core;

namespace FileService.Core.Grpc;

/// <summary>
/// Internal gRPC API: точечные синхронные запросы от других backend-сервисов к FileService.
/// </summary>
public sealed class FileInternalGrpcService : FileInternal.FileInternalBase
{
    private readonly GetMediaAssetInfoHandler _getMediaAssetInfoHandler;
    private readonly GetMediaAssetsInfoHandler _getMediaAssetsInfoHandler;
    private readonly CheckMediaAssetExistHandler _handler;

    public FileInternalGrpcService(
        GetMediaAssetInfoHandler getMediaAssetInfoHandler,
        GetMediaAssetsInfoHandler getMediaAssetsInfoHandler,
        CheckMediaAssetExistHandler handler)
    {
        _getMediaAssetInfoHandler = getMediaAssetInfoHandler;
        _getMediaAssetsInfoHandler = getMediaAssetsInfoHandler;
        _handler = handler;
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

        var result = await _handler.Handle(mediaAssetId, context.CancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new RpcException(new Status(StatusCode.Internal, "Failed to check media asset"));
        }

        return new CheckMediaAssetExistsReply
        {
            IsExist = result.Value.IsExist
        };
    }
}
