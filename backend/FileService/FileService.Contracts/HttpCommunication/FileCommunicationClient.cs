using System.Globalization;
using CSharpFunctionalExtensions;
using FileService.Contracts;
using FileService.Contracts.Grpc;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.Contracts.HttpCommunication;

/// <summary>
/// Adapter для общения с FileService через internal gRPC contract.
/// </summary>
internal sealed class FileCommunicationClient : IFileCommunicationService
{
    private readonly FileInternal.FileInternalClient _grpcClient;
    private readonly ILogger<FileCommunicationClient> _logger;

    public FileCommunicationClient(
        FileInternal.FileInternalClient grpcClient,
        ILogger<FileCommunicationClient> logger)
    {
        _grpcClient = grpcClient;
        _logger = logger;
    }

    public async Task<Result<GetMediaAssetResponse, Failure>> GetMediaAssetInfo(Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        try
        {
            var call = _grpcClient.GetMediaAssetInfoAsync(
                new GetMediaAssetInfoRequest
                {
                    MediaAssetId = mediaAssetId.ToString()
                },
                cancellationToken: cancellationToken);

            var reply = await call.ResponseAsync.ConfigureAwait(false);

            return new GetMediaAssetResponse(
                Guid.Parse(reply.Id),
                reply.Status,
                reply.AssetType,
                DateTime.Parse(reply.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                DateTime.Parse(reply.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reply.HasViewUrl ? reply.ViewUrl : null,
                reply.HasDownloadUrl ? reply.DownloadUrl : null,
                reply.HasThumbnailUrl ? reply.ThumbnailUrl : null,
                reply.HasSize ? reply.Size : null,
                reply.HasFileName ? reply.FileName : null,
                reply.HasContentType ? reply.ContentType : null);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC error getting media asset info for {MediaAssetId}", mediaAssetId);
            return Error.Failure("file-service.grpc", ex.Status.Detail).ToFailure();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media asset info for {MediaAssetId}", mediaAssetId);
            return Error.Failure("server.internal", "Failed to request media asset info").ToFailure();
        }
    }

    public async Task<Result<GetMediaAssetsResponse, Failure>> GetMediaAssetsInfo(GetMediaAssetsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var grpcRequest = new GetMediaAssetsInfoRequest();
            grpcRequest.MediaAssetIds.AddRange(request.MediaAssetIds.Select(id => id.ToString()));

            var call = _grpcClient.GetMediaAssetsInfoAsync(grpcRequest, cancellationToken: cancellationToken);
            var reply = await call.ResponseAsync.ConfigureAwait(false);

            var mediaAssets = reply.MediaAssets.Select(mediaAsset => new GetMediaAssetDto(
                    Guid.Parse(mediaAsset.Id),
                    mediaAsset.Status,
                    mediaAsset.AssetType,
                    mediaAsset.HasViewUrl ? mediaAsset.ViewUrl : null,
                    mediaAsset.HasDownloadUrl ? mediaAsset.DownloadUrl : null,
                    mediaAsset.HasThumbnailUrl ? mediaAsset.ThumbnailUrl : null))
                .ToList();

            return new GetMediaAssetsResponse(mediaAssets);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC error getting media asset info for {MediaAssetIds}", request.MediaAssetIds);
            return Error.Failure("file-service.grpc", ex.Status.Detail).ToFailure();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media asset info for {MediaAssetIds}", request.MediaAssetIds);
            return Error.Failure("server.internal", "Failed to request media asset info").ToFailure();
        }
    }

    public async Task<Result<CheckMediaAssetExistResponse, Failure>> CheckMediaAssetExists(Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        try
        {
            var call = _grpcClient.CheckMediaAssetExistsAsync(
                new CheckMediaAssetExistsRequest
                {
                    MediaAssetId = mediaAssetId.ToString()
                },
                cancellationToken: cancellationToken);

            var reply = await call.ResponseAsync.ConfigureAwait(false);

            return new CheckMediaAssetExistResponse(reply.IsExist);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC error checking media asset {MediaAssetId}", mediaAssetId);
            return Error.Failure("file-service.grpc", ex.Status.Detail).ToFailure();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking media asset {MediaAssetId}", mediaAssetId);
            return Error.Failure("server.internal", "Failed to check media asset").ToFailure();
        }
    }
}
