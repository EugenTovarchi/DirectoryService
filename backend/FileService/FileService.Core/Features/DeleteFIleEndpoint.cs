using CSharpFunctionalExtensions;
using FileService.Core.FilesStorage;
using FileService.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace FileService.Core.Features;

public sealed class DeleteFIleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/files/{mediaAssetId::guid}",
            async Task<EndpointResult<Guid>> (
                [FromRoute] Guid mediaAssetId,
                [FromServices] DeleteFIleHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(mediaAssetId, cancellationToken));
    }
}

public sealed class DeleteFIleHandler
{
    private readonly ILogger<DeleteFIleHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;

    public DeleteFIleHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<DeleteFIleHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
    }

    public async Task<Result<Guid, Failure>> Handle(Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        if (mediaAssetId == Guid.Empty)
            return Errors.General.ValueIsInvalid("MediaAssetId").ToFailure();

        var mediaAssetResult = await _mediaAssetsRepository.GetBy(m => m.Id == mediaAssetId, cancellationToken);
        if (mediaAssetResult.IsFailure)
        {
            _logger.LogInformation("Media assets not found");
            return Errors.General.NotFoundEntity("MediaAssetId").ToFailure();
        }

        var mediaAsset = mediaAssetResult.Value;

        if (mediaAsset.Status == MediaStatus.DELETED)
        {
            return Errors.General.ValueIsInvalid("media_asset_status").ToFailure();
        }

        Result<string, Error> deleteFilesFromS3Result = await _fileStorageProvider
            .DeleteFileAsync(mediaAsset.Key, cancellationToken);
        if (deleteFilesFromS3Result.IsFailure)
        {
            _logger.LogError("Error when try to delete files!");
            return deleteFilesFromS3Result.Error.ToFailure();
        }

        mediaAsset.MarkDeleted();

        var result = await _mediaAssetsRepository.SaveChangeAsync(cancellationToken);
        if (!result.IsFailure)
        {
            return mediaAsset.Id;
        }

        _logger.LogError("Error when try to save changes!");
        return result.Error.ToFailure();

    }
}