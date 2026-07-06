using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Core.Abstractions;
using FileService.Core.Authorization;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;
using SharedService.SharedKernel.Messaging.Files.Events;
using Wolverine;

namespace FileService.Core.Features;

public sealed class CompleteMultipartUploadEndpoint : IEndpoint
{
    private const string CORRELATION_ID_HEADER_NAME = "X-Correlation-Id";
    private const int MAX_CORRELATION_ID_LENGTH = 128;

    /// <summary>
    /// Завершает загрузку файла в S3.
    /// Выполняет отправку Id файла в DS сервис.
    /// Если видео, то начинает hls обработку через планировщик Quartz.
    /// </summary>
    /// <param name="app">FileService.</param>
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/multipart/end",
            async Task<EndpointResult> (
                [FromBody] CompleteMultipartUploadRequest request,
                [FromServices] CompleteMultipartUploadHandler handler,
                HttpContext httpContext,
                CancellationToken cancellationToken) => await handler.Handle(
                request,
                GetCorrelationId(httpContext),
                cancellationToken))
            .RequireAuthorization(FileAuthorizationPolicies.FILES_UPLOAD);
    }

    private static string GetCorrelationId(HttpContext httpContext)
    {
        string? headerValue = httpContext.Request.Headers[CORRELATION_ID_HEADER_NAME].FirstOrDefault();
        string correlationId = string.IsNullOrWhiteSpace(headerValue)
            ? httpContext.TraceIdentifier
            : headerValue;

        return correlationId.Length <= MAX_CORRELATION_ID_LENGTH
            ? correlationId
            : correlationId[..MAX_CORRELATION_ID_LENGTH];
    }
}

public sealed class CompleteMultipartUploadHandler
{
    private readonly ILogger<CompleteMultipartUploadHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IVideoProcessingScheduler _videoProcessingScheduler;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;
    private readonly IVideoProcessesRepository _videoProcessesRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IMessageBus _messageBus;
    private readonly IVideoProcessingPolicy _videoProcessingPolicy;

    public CompleteMultipartUploadHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<CompleteMultipartUploadHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository,
        ITransactionManager transactionManager,
        IVideoProcessingScheduler videoProcessingScheduler,
        IVideoProcessesRepository videoProcessesRepository,
        IVideoProcessingPolicy videoProcessingPolicy,
        IMessageBus messageBus)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
        _transactionManager = transactionManager;
        _videoProcessingScheduler = videoProcessingScheduler;
        _videoProcessesRepository = videoProcessesRepository;
        _videoProcessingPolicy = videoProcessingPolicy;
        _messageBus = messageBus;
    }

    public async Task<UnitResult<Failure>> Handle(
        CompleteMultipartUploadRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        Guid? videoAssetIdToSchedule = null;
        bool transactionCommitted = false;

        var transactionScopeResult = await _transactionManager.BeginTransactionAsync(cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using var transactionScope = transactionScopeResult.Value;
        try
        {
            var mediaAssetResult =
                await _mediaAssetsRepository.GetBy(m => m.Id == request.MediaAssetId, cancellationToken);
            if (mediaAssetResult.IsFailure)
                return mediaAssetResult.Error.ToFailure();

            MediaAsset mediaAsset = mediaAssetResult.Value;

            if (mediaAsset.MediaData.ExpectedChunkCount != request.PartETags.Count)
            {
                return Errors.General.ValueIsInvalid("Count of expected chunks are not equal to part etags count!")
                    .ToFailure();
            }

            Result<string, Error> completeResult =
                await _fileStorageProvider.CompleteMultipartUploadAsync(mediaAsset.UploadKey, request.UploadId,
                    request.PartETags,
                    cancellationToken);
            if (completeResult.IsFailure)
            {
                mediaAsset.MarkFailed();
                await _transactionManager.SaveChangeAsync(cancellationToken);

                var commitResult = transactionScope.Commit();
                if (commitResult.IsFailure)
                {
                    _logger.LogError(
                        "Failed to commit failed upload state for media asset {MediaAssetId}. Error code: {ErrorCode}",
                        mediaAsset.Id,
                        commitResult.Error.Code);
                }

                return completeResult.Error.ToFailure();
            }

            var markUploadedResult = mediaAsset.MarkUploaded();
            if (markUploadedResult.IsFailure)
            {
                _logger.LogError(
                    "Failed to mark media asset {MediaAssetId} as uploaded. Error code: {ErrorCode}",
                    mediaAsset.Id,
                    markUploadedResult.Error.Code);
                return markUploadedResult.Error.ToFailure();
            }

            if (!mediaAsset.RequiresProcessing())
            {
                mediaAsset.MarkReady();
            }

            var fileUploadedEvent = new FileUploaded(
                AssetId: mediaAsset.Id,
                FileName: mediaAsset.MediaData.FileName.Value,
                ContentType: mediaAsset.MediaData.ContentType.Value,
                Size: mediaAsset.MediaData.Size,
                AssetType: mediaAsset.AssetType.ToString(),
                TargetEntityId: mediaAsset.OwnerId,
                TargetEntityType: mediaAsset.OwnerType);

            await _messageBus.PublishAsync(fileUploadedEvent);

            var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
            if (saveResult.IsFailure)
            {
                _logger.LogError(
                    "Failed to persist FileUploaded outbox event for media asset {MediaAssetId}. Error code: {ErrorCode}",
                    mediaAsset.Id,
                    saveResult.Error.Code);
                return saveResult.Error.ToFailure();
            }

            _logger.LogInformation(
                "Persisted FileUploaded outbox event for media asset {MediaAssetId}",
                mediaAsset.Id);

            if (mediaAsset.RequiresProcessing() && mediaAsset.AssetType == AssetType.VIDEO)
            {
                var createVideoProcessResult = VideoProcess.Create(
                    mediaAsset.Id,
                    mediaAsset.UploadKey,
                    _videoProcessingPolicy.MaxRetries,
                    correlationId);
                if (createVideoProcessResult.IsFailure)
                {
                    _logger.LogError(
                        "Failed to create video process for media asset {MediaAssetId}. Error code: {ErrorCode}",
                        mediaAsset.Id,
                        createVideoProcessResult.Error.Code);

                    return createVideoProcessResult.Error.ToFailure();
                }

                var videoProcess = createVideoProcessResult.Value;
                _videoProcessesRepository.Add(videoProcess);

                var saveVideoProcessResult = await _transactionManager.SaveChangeAsync(cancellationToken);
                if (saveVideoProcessResult.IsFailure)
                {
                    _logger.LogError(
                        "Failed to persist video process for video asset {VideoAssetId}. Error code: {ErrorCode}",
                        mediaAsset.Id,
                        saveVideoProcessResult.Error.Code);
                    return saveVideoProcessResult.Error.ToFailure();
                }

                videoAssetIdToSchedule = videoProcess.VideoAssetId;
                _logger.LogInformation("Created pending video process for asset {VideoAssetId}",
                    videoProcess.VideoAssetId);
            }

            var finalCommitResult = transactionScope.Commit();
            if (finalCommitResult.IsFailure)
                return finalCommitResult.Error.ToFailure();
            transactionCommitted = true;

            if (videoAssetIdToSchedule.HasValue)
            {
                var scheduleResult = await _videoProcessingScheduler.ScheduleProcessingAsync(
                    videoAssetIdToSchedule.Value,
                    correlationId,
                    startAt: null,
                    cancellationToken: cancellationToken);
                if (scheduleResult.IsFailure)
                {
                    _logger.LogError(
                        "Failed to schedule processing for video asset {VideoAssetId}. Error code: {ErrorCode}",
                        videoAssetIdToSchedule.Value,
                        scheduleResult.Error.Code);
                }
            }

            _logger.LogInformation("Completed multipart upload for media asset {MediaAssetId}", mediaAsset.Id);

            return UnitResult.Success<Failure>();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while completing multipart upload for media asset {MediaAssetId}",
                request.MediaAssetId);
            if (!transactionCommitted)
                transactionScope.Rollback();
            return Error.Failure("unexpected", "Unexpected error while completing multipart upload").ToFailure();
        }
    }
}
