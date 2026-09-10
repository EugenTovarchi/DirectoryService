using CSharpFunctionalExtensions;
using FileService.Contracts.Responses;
using FileService.Core.Abstractions;
using FileService.Core.Authorization;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing.VO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace FileService.Core.Features;

public sealed class GetVideoInfoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/files/department/{videoId:guid}",
                async Task<EndpointResult<GetVideoInfoResponse>> (
                    [FromRoute] Guid videoId,
                    [FromServices] GetVideoInfoHandler handler,
                    CancellationToken cancellationToken) => await handler.Handle(videoId, cancellationToken))
            .RequireAuthorization(FileAuthorizationPolicies.FILES_READ);
    }
}

public sealed class GetVideoInfoHandler
{
    private readonly ILogger<GetVideoInfoHandler> _logger;
    private readonly IFileReadDbContext _fileReadDbContext;
    private readonly IVideoProcessesRepository _videoProcessesRepository;

    public GetVideoInfoHandler(
        ILogger<GetVideoInfoHandler> logger,
        IFileReadDbContext fileReadDbContext,
        IVideoProcessesRepository videoProcessesRepository)
    {
        _logger = logger;
        _fileReadDbContext = fileReadDbContext;
        _videoProcessesRepository = videoProcessesRepository;
    }

    public async Task<Result<GetVideoInfoResponse, Failure>> Handle(Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        if (mediaAssetId == Guid.Empty)
            return Errors.General.ValueIsInvalid("VideoId").ToFailure();

        MediaAsset? videoInfo = await _fileReadDbContext.ReadMediaAssets
            .FirstOrDefaultAsync(m => m.Id == mediaAssetId, cancellationToken);
        if (videoInfo == null)
        {
            _logger.LogDebug("Media asset {MediaAssetId} not found", mediaAssetId);
            return Errors.General.NotFoundEntity("mediaAssetId").ToFailure();
        }

        if (videoInfo.AssetType != AssetType.VIDEO)
        {
            return Error.Validation("incorrect.media_asset.type",
                "Asset type must be video").ToFailure();
        }

        var videoProcessResult = await _videoProcessesRepository.GetByVideoAssetId(
            mediaAssetId, cancellationToken);
        if (videoProcessResult.IsFailure)
        {
            _logger.LogDebug(
                "Failed to get video process for media asset {MediaAssetId}. Error code: {ErrorCode}",
                mediaAssetId,
                videoProcessResult.Error.Code);

            return videoProcessResult.Error.ToFailure();
        }

        VideoMetadata? metadata = videoProcessResult.Value.MetaData;

        var response = new GetVideoInfoResponse(
            videoInfo.Id,
            videoInfo.MediaData.FileName.Value,
            videoInfo.MediaData.ContentType.Value,
            videoInfo.Status.ToString().ToLowerInvariant(),
            videoInfo.CreatedAt,
            videoInfo.UpdatedAt,
            videoInfo.MediaData.Size,
            metadata?.Duration,
            metadata?.Width,
            metadata?.Height,
            metadata?.HasAudio);

        return response;
    }
}
