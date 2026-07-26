using CSharpFunctionalExtensions;
using FileService.Contracts;
using FileService.Contracts.Requests;
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

public sealed class GetMediaAssetsInfoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/batch",
            async Task<EndpointResult<GetMediaAssetsResponse>> (
                [FromBody] GetMediaAssetsRequest request,
                [FromServices] GetMediaAssetsInfoHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(request, cancellationToken))
            .RequireAuthorization(FileAuthorizationPolicies.FILES_READ);
    }
}

public sealed class GetMediaAssetsInfoHandler
{
    private readonly ILogger<GetMediaAssetsInfoHandler> _logger;
    private readonly IFileReadDbContext _fileReadDbContext;
    private readonly IFileStorageProvider _fileStorageProvider;

    public GetMediaAssetsInfoHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<GetMediaAssetsInfoHandler> logger,
        IFileReadDbContext fileReadDbContext)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _fileReadDbContext = fileReadDbContext;
    }

    public async Task<Result<GetMediaAssetsResponse, Failure>> Handle(GetMediaAssetsRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.MediaAssetIds.Any())
            return new GetMediaAssetsResponse([]);

        List<MediaAsset> mediaAssets = await _fileReadDbContext.ReadMediaAssets
            .Where(m => request.MediaAssetIds.Contains(m.Id))
            .ToListAsync(cancellationToken);
        if (mediaAssets.Count == 0)
        {
            _logger.LogInformation("No media assets found");
            return new GetMediaAssetsResponse([]);
        }

        var results = new List<GetMediaAssetDto>();

        foreach (MediaAsset mediaAsset in mediaAssets.Where(MediaAssetUrlBuilder.CanExposeMediaInfo))
        {
            var urlsResult = await MediaAssetUrlBuilder.BuildAsync(mediaAsset, _fileStorageProvider, cancellationToken);
            if (urlsResult.IsFailure)
                return urlsResult.Error.ToFailure();

            MediaAssetUrls urls = urlsResult.Value;

            var mediaAssetDto = new GetMediaAssetDto(
                mediaAsset.Id,
                mediaAsset.Status.ToString().ToLowerInvariant(),
                mediaAsset.AssetType.ToString().ToLowerInvariant(),
                urls.ViewUrl,
                urls.DownloadUrl,
                urls.ThumbnailUrl);

            results.Add(mediaAssetDto);
        }

        return new GetMediaAssetsResponse(results);
    }
}
