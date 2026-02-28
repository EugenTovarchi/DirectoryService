using CSharpFunctionalExtensions;
using FileService.Contracts;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Core.EndpointSettings;
using FileService.Core.FilesStorage;
using FileService.Core.Models;
using FileService.Domain;
using FileService.Domain.Assets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.Core.Features;

public sealed class GetMediaAssetsUploadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/get",
            async Task<EndpointResult> (
                [FromBody] GetMediaAssetsRequest request,
                [FromServices] GetMediaAssetsUploadHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(request, cancellationToken));
    }
}

public sealed class GetMediaAssetsUploadHandler
{
    private readonly ILogger<GetMediaAssetsUploadHandler> _logger;
    private readonly IFileReadDbContext _fileReadDbContext;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;

    public GetMediaAssetsUploadHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<GetMediaAssetsUploadHandler> logger,
        IFileReadDbContext fileReadDbContext,
        IMediaAssetsRepository mediaAssetsRepository)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _fileReadDbContext = fileReadDbContext;
        _mediaAssetsRepository = mediaAssetsRepository;
    }

    public async Task<Result<GetMediaAssetsResponse, Failure>> Handle(GetMediaAssetsRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.MediaAssetIds.Any())
            return new GetMediaAssetsResponse([]);

        List<MediaAsset> mediaAsset = await _fileReadDbContext.ReadMediaAssets
            .Where(m => request.MediaAssetIds.Contains(m.Id)
                        && m.Status != MediaStatus.DELETED)
            .ToListAsync(cancellationToken);

        List<MediaAsset> readyMediaAssets = mediaAsset.Where(m => m.Status == MediaStatus.READY).ToList();
        List<StorageKey> keys = readyMediaAssets.Select(m => m.Key).ToList();

        Result<IReadOnlyList<MediaUrl>, Error> urlsResult = await _fileStorageProvider
            .GenerateDownloadUrlsAsync(keys, cancellationToken);
        if (urlsResult.IsFailure)
            return urlsResult.Error.ToFailure();

        var urls = urlsResult.Value;

        var urlsDict = urls.ToDictionary(url => url.StorageKey, url => url.PresignedUrl);
        var results = new List<GetMediaAssetDto>();

        foreach (MediaAsset readyMediaAsset in readyMediaAssets)
        {
            urlsDict.TryGetValue(readyMediaAsset.Key, out string? url);

            var mediaAssetDto = new GetMediaAssetDto(
                readyMediaAsset.Id,
                readyMediaAsset.Status.ToString().ToLowerInvariant(),
                readyMediaAsset.AssetType.ToString().ToLowerInvariant(),
                url);

            results.Add(mediaAssetDto);
        }

        return new GetMediaAssetsResponse(results);
    }
}