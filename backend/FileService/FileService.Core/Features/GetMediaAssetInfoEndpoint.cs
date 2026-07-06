using CSharpFunctionalExtensions;
using FileService.Contracts.Responses;
using FileService.Core.Authorization;
using FileService.Core.FilesStorage;
using FileService.Domain.Assets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace FileService.Core.Features;

public sealed class GetMediaAssetInfoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/{mediaAssetId:guid}",
            async Task<EndpointResult<GetMediaAssetResponse>> (
                [FromRoute] Guid mediaAssetId,
                [FromServices] GetMediaAssetInfoHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(mediaAssetId, cancellationToken))
            .RequireAuthorization(FileAuthorizationPolicies.FILES_READ);
    }
}

public sealed class GetMediaAssetInfoHandler
{
    private readonly ILogger<GetMediaAssetInfoHandler> _logger;
    private readonly IFileReadDbContext _fileReadDbContext;
    private readonly IFileStorageProvider _fileStorageProvider;

    public GetMediaAssetInfoHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<GetMediaAssetInfoHandler> logger,
        IFileReadDbContext fileReadDbContext)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _fileReadDbContext = fileReadDbContext;
    }

    public async Task<Result<GetMediaAssetResponse, Failure>> Handle(Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        if (mediaAssetId == Guid.Empty)
            return Errors.General.ValueIsInvalid("MediaAssetId").ToFailure();

        MediaAsset? mediaAsset = await _fileReadDbContext.ReadMediaAssets
            .FirstOrDefaultAsync(m => m.Id == mediaAssetId, cancellationToken);
        if (mediaAsset == null)
        {
            _logger.LogInformation("Media assets not found");
            return Errors.General.NotFoundEntity("MediaAssetId").ToFailure();
        }

        if (!MediaAssetUrlBuilder.CanExposeMediaInfo(mediaAsset))
        {
            _logger.LogInformation("Media asset {MediaAssetId} is not ready for exposure", mediaAssetId);
            return Errors.General.NotFoundEntity("MediaAssetId").ToFailure();
        }

        var urlsResult = await MediaAssetUrlBuilder.BuildAsync(mediaAsset, _fileStorageProvider, cancellationToken);
        if (urlsResult.IsFailure)
        {
            _logger.LogError("Error when try to generate media asset urls!");
            return urlsResult.Error.ToFailure();
        }

        MediaAssetUrls urls = urlsResult.Value;

        return new GetMediaAssetResponse(
            mediaAsset.Id,
            mediaAsset.Status.ToString().ToLowerInvariant(),
            mediaAsset.AssetType.ToString().ToLowerInvariant(),
            mediaAsset.CreatedAt,
            mediaAsset.UpdatedAt,
            urls.ViewUrl,
            urls.DownloadUrl,
            urls.ThumbnailUrl,
            mediaAsset.MediaData.Size,
            mediaAsset.MediaData.FileName.Value,
            mediaAsset.MediaData.ContentType.Value);
    }
}
