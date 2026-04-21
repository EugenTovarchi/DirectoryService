using CSharpFunctionalExtensions;
using FileService.Core.Abstractions;
using FileService.Core.FilesStorage;
using FileService.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;
using SharedService.SharedKernel.Messaging.Files.Events;
using Wolverine;

namespace FileService.Core.Features;

public sealed class DeleteFileEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/files/{mediaAssetId:guid}",
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
    private readonly ITransactionManager _transactionManager;
    private readonly IMessageBus _messageBus;

    public DeleteFIleHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<DeleteFIleHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository,
        ITransactionManager transactionManager,
        IMessageBus messageBus)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
        _transactionManager = transactionManager;
        _messageBus = messageBus;
    }

    public async Task<Result<Guid, Failure>> Handle(Guid mediaAssetId,
        CancellationToken cancellationToken)
    {
        if (mediaAssetId == Guid.Empty)
            return Errors.General.ValueIsInvalid("MediaAssetId").ToFailure();

        var transactionScopeResult = await _transactionManager.BeginTransactionAsync(cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using var transactionScope = transactionScopeResult.Value;
        try
        {
            var mediaAssetResult = await _mediaAssetsRepository.GetBy(m => m.Id == mediaAssetId, cancellationToken);
            if (mediaAssetResult.IsFailure)
            {
                _logger.LogInformation("Media assets not found");
                return Errors.General.NotFoundEntity("MediaAssetId").ToFailure();
            }

            var mediaAsset = mediaAssetResult.Value;

            var statusCheckResult = mediaAsset.Status switch
            {
                MediaStatus.UPLOADING => Error.Failure("media_asset.invalid_status",
                    "Cannot delete file in UPLOADING status. Use cancel multipart upload instead."),

                MediaStatus.PROCESSING => Error.Failure("media_asset.invalid_status",
                    "Cannot delete file while it's being processed."),

                MediaStatus.DELETED => Errors.General.ValueIsInvalid("media_asset_status.already_deleted"),

                MediaStatus.UPLOADED or MediaStatus.READY or MediaStatus.FAILED => null,

                _ => Error.Failure("media_asset.unknown_status", $"Unknown status: {mediaAsset.Status}")
            };

            if (statusCheckResult != null)
                return statusCheckResult.ToFailure();

            Result<string, Error> deleteFilesFromS3Result = await _fileStorageProvider
                .DeleteFileAsync(mediaAsset.UploadKey, cancellationToken);
            if (deleteFilesFromS3Result.IsFailure)
            {
                _logger.LogError("Error when try to delete files!");
                return deleteFilesFromS3Result.Error.ToFailure();
            }

            mediaAsset.MarkDeleted();

            var fileDeletedEvent = new FileDeleted(
                AssetId: mediaAsset.Id,
                AssetType: mediaAsset.AssetType.ToString(),
                TargetEntityId: mediaAsset.OwnerId,
                TargetEntityType: mediaAsset.OwnerType);

            await _messageBus.PublishAsync(fileDeletedEvent);
            _logger.LogInformation("Published FileDeleted to outbox for {AssetId}", mediaAsset.Id);

            var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
            if (saveResult.IsFailure)
            {
                _logger.LogError("Error when try to save changes!");
                return saveResult.Error.ToFailure();
            }

            var commitResult = transactionScope.Commit();
            if (commitResult.IsFailure)
            {
                _logger.LogError("Failed to commit transaction");
                return commitResult.Error.ToFailure();
            }

            _logger.LogInformation("Successfully deleted file {AssetId}", mediaAsset.Id);
            return mediaAsset.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error");
            transactionScope.Rollback();
            return Error.Failure("unexpected", ex.Message).ToFailure();
        }
    }
}