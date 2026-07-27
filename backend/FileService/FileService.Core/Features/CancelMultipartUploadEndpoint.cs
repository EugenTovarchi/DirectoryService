using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
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

public sealed class CancelMultipartUploadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/multipart/cancel",
            async Task<EndpointResult> (
                [FromBody] CancelMultipartUploadRequest request,
                [FromServices] CancelMultipartUploadHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(request, cancellationToken))
            .RequireAuthorization(FileAuthorizationPolicies.FILES_UPLOAD);
    }
}

public sealed class CancelMultipartUploadHandler
{
    private readonly ILogger<CancelMultipartUploadHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;
    private readonly IMultipartUploadSessionsRepository _uploadSessionsRepository;
    private readonly ITransactionManager _transactionManager;

    public CancelMultipartUploadHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<CancelMultipartUploadHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository,
        IMultipartUploadSessionsRepository uploadSessionsRepository,
        ITransactionManager transactionManager)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
        _uploadSessionsRepository = uploadSessionsRepository;
        _transactionManager = transactionManager;
    }

    public async Task<UnitResult<Failure>> Handle(CancelMultipartUploadRequest request,
        CancellationToken cancellationToken)
    {
        var mediaAssetResult = await _mediaAssetsRepository.GetBy(
            m => m.Id == request.MediaAssetId, cancellationToken);
        if (mediaAssetResult.IsFailure)
        {
            _logger.LogError("Media asset with id {Id} was not found", request.MediaAssetId);
            return mediaAssetResult.Error.ToFailure();
        }

        MediaAsset mediaAsset = mediaAssetResult.Value;

        if (mediaAsset.Status != MediaStatus.UPLOADING)
        {
            return Error.Failure("media.upload.not_in_progress",
                $"Cannot abort upload for media in status: {mediaAsset.Status}").ToFailure();
        }

        Result<bool, Error> claimResult = await _uploadSessionsRepository.TryBeginAbortAsync(
            request.MediaAssetId,
            request.UploadId,
            cancellationToken);
        if (claimResult.IsFailure)
            return claimResult.Error.ToFailure();

        if (!claimResult.Value)
        {
            Result<MultipartUploadSession, Error> currentSessionResult = await _uploadSessionsRepository
                .GetByMediaAssetIdAsync(request.MediaAssetId, cancellationToken);
            if (currentSessionResult.IsFailure)
                return currentSessionResult.Error.ToFailure();

            MultipartUploadSession currentSession = currentSessionResult.Value;
            if (currentSession.Status == MultipartUploadStatus.ABORTED)
                return UnitResult.Success<Failure>();

            return Error.Conflict(
                "multipart.upload.operation_in_progress",
                $"Multipart upload is in {currentSession.Status} status").ToFailure();
        }

        UnitResult<Error> abortResult =
            await _fileStorageProvider.AbortMultipartUploadAsync(mediaAsset.UploadKey, request.UploadId, cancellationToken);
        if (abortResult.IsFailure && abortResult.Error.Code != "upload.id")
        {
            await HandleAbortFailureAsync(mediaAsset, abortResult.Error, cancellationToken);
            return abortResult.Error.ToFailure();
        }

        Result<MultipartUploadSession, Error> sessionResult = await _uploadSessionsRepository
            .GetByMediaAssetIdAsync(request.MediaAssetId, cancellationToken);
        if (sessionResult.IsFailure)
            return sessionResult.Error.ToFailure();

        UnitResult<Error> markAbortedResult = sessionResult.Value.Abort();
        if (markAbortedResult.IsFailure)
            return markAbortedResult.Error.ToFailure();

        UnitResult<Error> markFailedResult = mediaAsset.MarkFailed();
        if (markFailedResult.IsFailure)
            return markFailedResult.Error.ToFailure();

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        _logger.LogInformation("Uploading media: {Id} was aborted", request.MediaAssetId);

        return UnitResult.Success<Failure>();
    }

    /// <summary>
    /// После ошибки S3 либо разрешает повтор отмены, либо сохраняет
    /// окончательную ошибку в зависимости от её типа.
    /// </summary>
    private async Task HandleAbortFailureAsync(
        MediaAsset mediaAsset,
        Error abortError,
        CancellationToken cancellationToken)
    {
        using var recoveryTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        CancellationToken recoveryToken = cancellationToken.IsCancellationRequested
            ? recoveryTokenSource.Token
            : cancellationToken;

        Result<MultipartUploadSession, Error> sessionResult = await _uploadSessionsRepository
            .GetByMediaAssetIdAsync(mediaAsset.Id, recoveryToken);
        if (sessionResult.IsFailure)
            return;

        bool retryable = StorageErrorClassifier.IsRetryable(abortError);

        UnitResult<Error> updateSessionResult = retryable
            ? sessionResult.Value.ReleaseAbort($"Retryable S3 abort error with code {abortError.Code}")
            : sessionResult.Value.Fail($"S3 abort failed with code {abortError.Code}");
        if (updateSessionResult.IsFailure)
            return;

        if (!retryable)
        {
            UnitResult<Error> markFailedResult = mediaAsset.MarkFailed();
            if (markFailedResult.IsFailure)
                return;
        }

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(recoveryToken);
        if (saveResult.IsFailure)
        {
            _logger.LogError(
                "Failed to persist abort failure for media asset {MediaAssetId}. Error code: {ErrorCode}",
                mediaAsset.Id,
                saveResult.Error.Code);
        }
    }
}
