using CSharpFunctionalExtensions;
using FileService.Contracts;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Core.Abstractions;
using FileService.Core.Authorization;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.Uploads;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace FileService.Core.Features;

public sealed class StartMultipartUploadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/multipart/start",
            async Task<EndpointResult<StartMultipartUploadResponse>> (
                [FromBody] StartMultipartUploadRequest request,
                [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
                [FromServices] StartMultipartUploadHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(
                request,
                idempotencyKey,
                cancellationToken))
            .RequireAuthorization(FileAuthorizationPolicies.FILES_UPLOAD);
    }
}

public sealed class StartMultipartUploadHandler
{
    private readonly ILogger<StartMultipartUploadHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IChunkSizeCalculator _chunkSizeCalculator;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;
    private readonly IMultipartUploadSessionsRepository _uploadSessionsRepository;
    private readonly ITransactionManager _transactionManager;

    public StartMultipartUploadHandler(
        IFileStorageProvider fileStorageProvider,
        IChunkSizeCalculator chunkSizeCalculator,
        ILogger<StartMultipartUploadHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository,
        IMultipartUploadSessionsRepository uploadSessionsRepository,
        ITransactionManager transactionManager)
    {
        _fileStorageProvider = fileStorageProvider;
        _chunkSizeCalculator = chunkSizeCalculator;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
        _uploadSessionsRepository = uploadSessionsRepository;
        _transactionManager = transactionManager;
    }

    public async Task<Result<StartMultipartUploadResponse, Failure>> Handle(
        StartMultipartUploadRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var fileNameResult = FileName.Create(request.FileName);
        if (fileNameResult.IsFailure)
            return fileNameResult.Error.ToFailure();

        var contentTypeResult = ContentType.Create(request.ContentType);
        if (contentTypeResult.IsFailure)
            return contentTypeResult.Error.ToFailure();

        var chunkCalculatorResult = _chunkSizeCalculator.CalculateChunkSize(request.Size);
        if (chunkCalculatorResult.IsFailure)
            return chunkCalculatorResult.Error.ToFailure();

        var mediaDataResult = MediaData.Create(
            fileNameResult.Value,
            contentTypeResult.Value,
            request.Size,
            chunkCalculatorResult.Value.TotalChunks);
        if (mediaDataResult.IsFailure)
            return mediaDataResult.Error.ToFailure();

        if (!FileAssetTypes.IsSupported(request.AssetType))
        {
            return Error.Validation("file.invalid.asset-type", "AssetType is not supported").ToFailure();
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Error.Validation(
                "multipart.upload.idempotency_key.required",
                "Idempotency-Key header is required").ToFailure();
        }

        string normalizedIdempotencyKey = idempotencyKey.Trim();
        Result<StartMultipartUploadResponse, Failure>? existingUploadResult =
            await TryResumeUploadAsync(
                request,
                normalizedIdempotencyKey,
                fileNameResult.Value,
                contentTypeResult.Value,
                chunkCalculatorResult.Value.ChunkSize,
                cancellationToken);
        if (existingUploadResult.HasValue)
            return existingUploadResult.Value;

        var mediaAssetResult = MediaAsset.CreateForUpload(
            mediaDataResult.Value,
            request.AssetType.ToAssetType(),
            request.OwnerId,
            request.OwnerType);

        if (mediaAssetResult.IsFailure)
            return mediaAssetResult.Error.ToFailure();

        var uploadSessionResult = MultipartUploadSession.Create(
            mediaAssetResult.Value.Id,
            normalizedIdempotencyKey);
        if (uploadSessionResult.IsFailure)
            return uploadSessionResult.Error.ToFailure();

        var startUploadResult = await _fileStorageProvider.StartMultipartUploadAsync(
            mediaAssetResult.Value.UploadKey,
            mediaAssetResult.Value.MediaData,
            cancellationToken);
        if (startUploadResult.IsFailure)
            return startUploadResult.Error.ToFailure();

        var activateSessionResult = uploadSessionResult.Value.Activate(
            startUploadResult.Value.UploadId,
            startUploadResult.Value.ExpiresAt);
        if (activateSessionResult.IsFailure)
            return activateSessionResult.Error.ToFailure();

        var chunkUploadUrlResult = await _fileStorageProvider.GenerateAllChunksUploadUrlsAsync(
            mediaAssetResult.Value.UploadKey,
            startUploadResult.Value.UploadId,
            chunkCalculatorResult.Value.TotalChunks,
            cancellationToken);
        if (chunkUploadUrlResult.IsFailure)
        {
            await AbortStartedUploadAsync(
                mediaAssetResult.Value,
                startUploadResult.Value.UploadId,
                cancellationToken);
            return chunkUploadUrlResult.Error.ToFailure();
        }

        _mediaAssetsRepository.Add(mediaAssetResult.Value);
        _uploadSessionsRepository.Add(uploadSessionResult.Value);
        mediaAssetResult.Value.MarkUploading();
        var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
        {
            _logger.LogError(
                "Failed to persist multipart upload state for media asset {MediaAssetId}. Error code: {ErrorCode}",
                mediaAssetResult.Value.Id,
                saveResult.Error.Code);

            await AbortStartedUploadAsync(
                mediaAssetResult.Value,
                startUploadResult.Value.UploadId,
                cancellationToken);
            return saveResult.Error.ToFailure();
        }

        _logger.LogInformation(
            "Started multipart upload for media asset {MediaAssetId} with {ChunkCount} chunks",
            mediaAssetResult.Value.Id,
            chunkCalculatorResult.Value.TotalChunks);

        return new StartMultipartUploadResponse(
            mediaAssetResult.Value.Id,
            startUploadResult.Value.UploadId,
            chunkUploadUrlResult.Value,
            chunkCalculatorResult.Value.TotalChunks,
            chunkCalculatorResult.Value.ChunkSize);
    }

    /// <summary>
    /// Обрабатывает повторный start-запрос с тем же Idempotency-Key:
    /// проверяет совпадение параметров и возвращает новые подписанные ссылки существующей сессии.
    /// </summary>
    private async Task<Result<StartMultipartUploadResponse, Failure>?> TryResumeUploadAsync(
        StartMultipartUploadRequest request,
        string idempotencyKey,
        FileName fileName,
        ContentType contentType,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey.Length > MultipartUploadSession.MAX_IDEMPOTENCY_KEY_LENGTH)
        {
            return Error.Validation(
                "multipart.upload.idempotency_key.invalid",
                $"Idempotency key must not exceed {MultipartUploadSession.MAX_IDEMPOTENCY_KEY_LENGTH} characters")
                .ToFailure();
        }

        MultipartUploadSession? session = await _uploadSessionsRepository.FindByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken);
        if (session is null)
            return null;

        Result<MediaAsset, Error> mediaAssetResult = await _mediaAssetsRepository.GetById(
            session.MediaAssetId,
            cancellationToken);
        if (mediaAssetResult.IsFailure)
            return mediaAssetResult.Error.ToFailure();

        MediaAsset mediaAsset = mediaAssetResult.Value;
        bool requestMatches = mediaAsset.MediaData.FileName.Value == fileName.Value
            && mediaAsset.MediaData.ContentType.Value == contentType.Value
            && mediaAsset.MediaData.Size == request.Size
            && mediaAsset.AssetType == request.AssetType.ToAssetType()
            && mediaAsset.OwnerId == request.OwnerId
            && string.Equals(mediaAsset.OwnerType, request.OwnerType, StringComparison.Ordinal);
        if (!requestMatches)
        {
            return Error.Conflict(
                "multipart.upload.idempotency_key.reused",
                "Idempotency key was already used for another upload request").ToFailure();
        }

        if (session.Status != MultipartUploadStatus.ACTIVE
            || string.IsNullOrWhiteSpace(session.UploadId)
            || session.ExpiresAt <= DateTime.UtcNow)
        {
            return Error.Conflict(
                "multipart.upload.cannot_resume",
                $"Multipart upload is in {session.Status} status").ToFailure();
        }

        Result<IReadOnlyList<ChunkUploadUrl>, Error> urlsResult = await _fileStorageProvider
            .GenerateAllChunksUploadUrlsAsync(
                mediaAsset.UploadKey,
                session.UploadId,
                mediaAsset.MediaData.ExpectedChunkCount,
                cancellationToken);
        if (urlsResult.IsFailure)
            return urlsResult.Error.ToFailure();

        return new StartMultipartUploadResponse(
            mediaAsset.Id,
            session.UploadId,
            urlsResult.Value,
            mediaAsset.MediaData.ExpectedChunkCount,
            chunkSize);
    }

    /// <summary>
    /// Компенсирует ошибку после создания upload в S3, чтобы не оставлять
    /// незавершённую загрузку без соответствующей записи в базе данных.
    /// </summary>
    private async Task AbortStartedUploadAsync(
        MediaAsset mediaAsset,
        string uploadId,
        CancellationToken cancellationToken)
    {
        using var cleanupTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        CancellationToken cleanupToken = cancellationToken.IsCancellationRequested
            ? cleanupTokenSource.Token
            : cancellationToken;

        UnitResult<Error> abortResult = await _fileStorageProvider.AbortMultipartUploadAsync(
            mediaAsset.UploadKey,
            uploadId,
            cleanupToken);

        if (abortResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to compensate multipart upload for media asset {MediaAssetId}. Error code: {ErrorCode}",
                mediaAsset.Id,
                abortResult.Error.Code);
        }
    }
}
