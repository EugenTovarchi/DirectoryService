using CSharpFunctionalExtensions;
using FileService.Contracts;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Core.FilesStorage;
using FileService.Core.Models;
using FileService.Domain;
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
                CancellationToken cancellationToken) => await handler.Handle(request, cancellationToken));
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

        List<MediaAsset> readyMediaAssets = await _fileReadDbContext.ReadMediaAssets
            .Where(m => request.MediaAssetIds.Contains(m.Id)
                        && m.Status == MediaStatus.UPLOADED)
            .ToListAsync(cancellationToken);
        if (readyMediaAssets.Count == 0)
        {
            _logger.LogInformation("No ready media assets found");
            return new GetMediaAssetsResponse([]);
        }

        List<StorageKey> keys = readyMediaAssets.Select(m => m.Key).ToList()!;

        Result<IReadOnlyList<MediaUrl>, Error> urlsResult = await _fileStorageProvider
            .GenerateDownloadUrlsAsync(keys, cancellationToken);
        if (urlsResult.IsFailure)
        {
            _logger.LogError("Error when try to generate download urls!");
            return urlsResult.Error.ToFailure();
        }

        var urls = urlsResult.Value;

        var urlsDict = urls.ToDictionary(url => url.StorageKey, url => url.PresignedUrl);
        var results = new List<GetMediaAssetDto>();

        foreach (MediaAsset readyMediaAsset in readyMediaAssets)
        {
            string? downloadUrl = null;

            if (urlsDict.TryGetValue(readyMediaAsset.Key!, out string? url))
            {
                downloadUrl = url;
            }

            var mediaAssetDto = new GetMediaAssetDto(
                readyMediaAsset.Id,
                readyMediaAsset.Status.ToString().ToLowerInvariant(),
                readyMediaAsset.AssetType.ToString().ToLowerInvariant(),
                downloadUrl);

            results.Add(mediaAssetDto);
        }

        return new GetMediaAssetsResponse(results);
    }
}