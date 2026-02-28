using CSharpFunctionalExtensions;
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
    private readonly IFileReadDbContext _fileReadDbContext;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;

    public DeleteFIleHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<DeleteFIleHandler> logger,
        IFileReadDbContext fileReadDbContext,
        IMediaAssetsRepository mediaAssetsRepository)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _fileReadDbContext = fileReadDbContext;
        _mediaAssetsRepository = mediaAssetsRepository;
    }

    public async Task<Result<Guid, Failure>> Handle(Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        if (mediaAssetId == Guid.Empty)
            return Errors.General.ValueIsInvalid("MediaAssetId").ToFailure();

        MediaAsset? mediaAsset = await _fileReadDbContext.ReadMediaAssets
            .FirstOrDefaultAsync(m => m.Id == mediaAssetId
                                      && m.Status != MediaStatus.DELETED, cancellationToken);
        if (mediaAsset == null)
        {
            _logger.LogInformation("Media assets not found");
            return Errors.General.NotFoundEntity("MediaAssetId").ToFailure();
        }

        mediaAsset.MarkDeleted();

        await _mediaAssetsRepository.SaveChangeAsync(cancellationToken);

        Result<string, Error> deleteFilesFromS3Result = await _fileStorageProvider
            .DeleteFileAsync(mediaAsset.Key, cancellationToken);
        if (deleteFilesFromS3Result.IsFailure)
        {
            _logger.LogError("Error when try to delete files!");
            return deleteFilesFromS3Result.Error.ToFailure();
        }

        return mediaAsset.Id;
    }
}