using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Core.Abstractions;
using FileService.Core.Authorization;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing;
using FileService.Domain.Uploads;
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
    private readonly IMultipartUploadSessionsRepository _uploadSessionsRepository;
    private readonly IVideoProcessesRepository _videoProcessesRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IMessageBus _messageBus;
    private readonly IVideoProcessingPolicy _videoProcessingPolicy;

    public CompleteMultipartUploadHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<CompleteMultipartUploadHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository,
        IMultipartUploadSessionsRepository uploadSessionsRepository,
        ITransactionManager transactionManager,
        IVideoProcessingScheduler videoProcessingScheduler,
        IVideoProcessesRepository videoProcessesRepository,
        IVideoProcessingPolicy videoProcessingPolicy,
        IMessageBus messageBus)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
        _uploadSessionsRepository = uploadSessionsRepository;
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

        Result<MediaAsset, Error> mediaAssetResult =
            await _mediaAssetsRepository.GetBy(m => m.Id == request.MediaAssetId, cancellationToken);
        if (mediaAssetResult.IsFailure)
            return mediaAssetResult.Error.ToFailure();

        MediaAsset mediaAsset = mediaAssetResult.Value;

        if (mediaAsset.MediaData.ExpectedChunkCount != request.PartETags.Count)
        {
            return Errors.General.ValueIsInvalid("Count of expected chunks are not equal to part etags count!")
                .ToFailure();
        }

        Result<bool, Error> claimResult = await _uploadSessionsRepository.TryBeginCompletionAsync(
            request.MediaAssetId,
            request.UploadId,
            cancellationToken);
        if (claimResult.IsFailure)
            return claimResult.Error.ToFailure();

        if (!claimResult.Value)
        {
            return await HandleCompletionConflictAsync(request, mediaAsset, cancellationToken);
        }

        Result<string, Error> completeResult = await _fileStorageProvider.CompleteMultipartUploadAsync(
            mediaAsset.UploadKey,
            request.UploadId,
            request.PartETags,
            cancellationToken);
        if (completeResult.IsFailure)
        {
            using var recoveryTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Result<bool, Error> fileExistsResult = await _fileStorageProvider.FileExistsAsync(
                mediaAsset.UploadKey,
                recoveryTokenSource.Token);

            if (fileExistsResult.IsFailure || !fileExistsResult.Value)
            {
                if (StorageErrorClassifier.IsRetryable(completeResult.Error))
                {
                    await ReleaseCompletionAsync(
                        mediaAsset.Id,
                        completeResult.Error,
                        recoveryTokenSource.Token);
                }
                else
                {
                    await MarkCompletionFailedAsync(
                        mediaAsset,
                        completeResult.Error,
                        recoveryTokenSource.Token);
                }

                return completeResult.Error.ToFailure();
            }

            _logger.LogWarning(
                "S3 completion returned an error but object exists for media asset {MediaAssetId}; finalizing local state",
                mediaAsset.Id);
        }

        Result<MultipartUploadSession, Error> sessionResult = await _uploadSessionsRepository
            .GetByMediaAssetIdAsync(request.MediaAssetId, cancellationToken);
        if (sessionResult.IsFailure)
            return sessionResult.Error.ToFailure();

        var transactionScopeResult = await _transactionManager.BeginTransactionAsync(cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using var transactionScope = transactionScopeResult.Value;
        try
        {
            UnitResult<Error> completeSessionResult = sessionResult.Value.Complete();
            if (completeSessionResult.IsFailure)
                return completeSessionResult.Error.ToFailure();

            UnitResult<Error> markUploadedResult = mediaAsset.MarkUploaded();
            if (markUploadedResult.IsFailure)
                return markUploadedResult.Error.ToFailure();

            if (!mediaAsset.RequiresProcessing())
            {
                UnitResult<Error> markReadyResult = mediaAsset.MarkReady();
                if (markReadyResult.IsFailure)
                    return markReadyResult.Error.ToFailure();
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

            if (mediaAsset.RequiresProcessing() && mediaAsset.AssetType == AssetType.VIDEO)
            {
                Result<VideoProcess, Error> createVideoProcessResult = VideoProcess.Create(
                    mediaAsset.Id,
                    mediaAsset.UploadKey,
                    _videoProcessingPolicy.MaxRetries,
                    correlationId);
                if (createVideoProcessResult.IsFailure)
                    return createVideoProcessResult.Error.ToFailure();

                _videoProcessesRepository.Add(createVideoProcessResult.Value);
                videoAssetIdToSchedule = createVideoProcessResult.Value.VideoAssetId;
            }

            UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
            if (saveResult.IsFailure)
                return saveResult.Error.ToFailure();

            UnitResult<Error> commitResult = transactionScope.Commit();
            if (commitResult.IsFailure)
                return commitResult.Error.ToFailure();
        }
        catch (Exception ex)
        {
            transactionScope.Rollback();
            _logger.LogError(
                ex,
                "Unexpected error while finalizing multipart upload for media asset {MediaAssetId}",
                request.MediaAssetId);
            return Error.Failure("unexpected", "Unexpected error while completing multipart upload").ToFailure();
        }

        if (videoAssetIdToSchedule.HasValue)
        {
            UnitResult<Error> scheduleResult = await _videoProcessingScheduler.ScheduleProcessingAsync(
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

    /// <summary>
    /// Разбирает ситуацию, когда право на завершение не получено:
    /// повтор уже завершённой операции считается успешным, а активная операция возвращает конфликт.
    /// </summary>
    private async Task<UnitResult<Failure>> HandleCompletionConflictAsync(
        CompleteMultipartUploadRequest request,
        MediaAsset mediaAsset,
        CancellationToken cancellationToken)
    {
        Result<MultipartUploadSession, Error> sessionResult = await _uploadSessionsRepository
            .GetByMediaAssetIdAsync(request.MediaAssetId, cancellationToken);
        if (sessionResult.IsFailure)
            return sessionResult.Error.ToFailure();

        MultipartUploadSession session = sessionResult.Value;
        if (!string.Equals(session.UploadId, request.UploadId, StringComparison.Ordinal))
        {
            return Errors.Validation.RecordIsInvalid("multipart_upload_session").ToFailure();
        }

        if (session.Status == MultipartUploadStatus.COMPLETED
            && mediaAsset.Status is MediaStatus.UPLOADED or MediaStatus.PROCESSING or MediaStatus.READY)
        {
            return UnitResult.Success<Failure>();
        }

        return Error.Conflict(
            "multipart.upload.operation_in_progress",
            $"Multipart upload is in {session.Status} status").ToFailure();
    }

    /// <summary>
    /// Сохраняет невосстановимую ошибку завершения и переводит файл в FAILED.
    /// </summary>
    private async Task MarkCompletionFailedAsync(
        MediaAsset mediaAsset,
        Error completionError,
        CancellationToken cancellationToken)
    {
        Result<MultipartUploadSession, Error> sessionResult = await _uploadSessionsRepository
            .GetByMediaAssetIdAsync(mediaAsset.Id, cancellationToken);
        if (sessionResult.IsFailure)
            return;

        UnitResult<Error> failSessionResult = sessionResult.Value.Fail(
            $"S3 multipart completion failed with code {completionError.Code}");
        UnitResult<Error> failMediaResult = mediaAsset.MarkFailed();
        if (failSessionResult.IsFailure || failMediaResult.IsFailure)
            return;

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
        {
            _logger.LogError(
                "Failed to persist failed multipart upload state for media asset {MediaAssetId}. Error code: {ErrorCode}",
                mediaAsset.Id,
                saveResult.Error.Code);
        }
    }

    /// <summary>
    /// Возвращает сессию в ACTIVE после временной ошибки S3,
    /// чтобы клиент мог безопасно повторить завершение.
    /// </summary>
    private async Task ReleaseCompletionAsync(
        Guid mediaAssetId,
        Error completionError,
        CancellationToken cancellationToken)
    {
        Result<MultipartUploadSession, Error> sessionResult = await _uploadSessionsRepository
            .GetByMediaAssetIdAsync(mediaAssetId, cancellationToken);
        if (sessionResult.IsFailure)
            return;

        UnitResult<Error> releaseResult = sessionResult.Value.ReleaseCompletion(
            $"Retryable S3 completion error with code {completionError.Code}");
        if (releaseResult.IsFailure)
            return;

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
        {
            _logger.LogError(
                "Failed to release multipart completion for media asset {MediaAssetId}. Error code: {ErrorCode}",
                mediaAssetId,
                saveResult.Error.Code);
        }
    }

}
