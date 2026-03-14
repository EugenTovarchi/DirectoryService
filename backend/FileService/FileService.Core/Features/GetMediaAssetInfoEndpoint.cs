using CSharpFunctionalExtensions;
using FileService.Contracts.Responses;
using FileService.Core.FilesStorage;
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

public sealed class GetMediaAssetInfoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/{mediaAssetId:guid}",
            async Task<EndpointResult<GetMediaAssetResponse>> (
                [FromRoute] Guid mediaAssetId,
                [FromServices] GetMediaAssetInfoHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(mediaAssetId, cancellationToken));
    }
}

public sealed class GetMediaAssetInfoHandler
{
    private readonly ILogger<GetMediaAssetsInfoHandler> _logger;
    private readonly IFileReadDbContext _fileReadDbContext;
    private readonly IFileStorageProvider _fileStorageProvider;

    public GetMediaAssetInfoHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<GetMediaAssetsInfoHandler> logger,
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
            return Result.Success<GetMediaAssetResponse, Failure>(null!);

        MediaAsset? mediaAsset = await _fileReadDbContext.ReadMediaAssets
            .FirstOrDefaultAsync(m => m.Id == mediaAssetId
                                      && m.Status == MediaStatus.UPLOADED, cancellationToken);
        if (mediaAsset == null)
        {
            _logger.LogInformation("Media assets not found");
            return Result.Success<GetMediaAssetResponse, Failure>(null!);
        }

        Result<string, Error> urlResult = await _fileStorageProvider
            .GenerateDownloadUrlAsync(mediaAsset.Key, cancellationToken);
        if (urlResult.IsFailure)
        {
            _logger.LogError("Error when try to generate download url!");
            return urlResult.Error.ToFailure();
        }

        string? url = urlResult.Value;

        return new GetMediaAssetResponse(
            mediaAsset.Id,
            mediaAsset.Status.ToString().ToLowerInvariant(),
            mediaAsset.AssetType.ToString().ToLowerInvariant(),
            mediaAsset.CreatedAt,
            mediaAsset.UpdatedAt,
            url,
            mediaAsset.MediaData.Size,
            mediaAsset.MediaData.FileName.Value,
            mediaAsset.MediaData.ContentType.Value);
    }
}