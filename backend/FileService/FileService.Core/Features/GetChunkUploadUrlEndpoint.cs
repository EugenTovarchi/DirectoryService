using Amazon.S3.Model;
using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Core.Abstractions;
using FileService.Core.Authorization;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.Uploads;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharedService.Core.Validation;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace FileService.Core.Features;

// Получить presigned URL для догрузки конкретного чанка.
public class GetChunkUploadUrlEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/chunk-upload/url",
            async Task<EndpointResult<GetChunkUploadUrlResponse>> (
                [FromBody] GetChunkUploadUrlRequest request,
                [FromServices] GetChunkUploadUrlHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(request, cancellationToken))
            .RequireAuthorization(FileAuthorizationPolicies.FILES_UPLOAD);
    }
}

public class GetChunkUploadUrlValidator : AbstractValidator<GetChunkUploadUrlRequest>
{
    public GetChunkUploadUrlValidator()
    {
        RuleFor(r => r.MediaAssetId).NotEmpty().WithError(Errors.General.NotFoundValue());

        RuleFor(r => r.UploadId)
            .NotEmpty().WithError(Errors.General.ValueIsEmptyOrWhiteSpace("UploadId"));

        RuleFor(r => r.PartNumber)
            .NotEmpty().WithError(Errors.General.ValueIsEmptyOrWhiteSpace("PartNumber"))
            .GreaterThan(0).WithError(Errors.General.ValueMustBePositive("PartNumber"))
            .LessThanOrEqualTo(100).WithError(Errors.General.ValueIsInvalid("PartNumber"));
    }
}

public sealed class GetChunkUploadUrlHandler
{
    private readonly ILogger<GetChunkUploadUrlHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;
    private readonly IMultipartUploadSessionsRepository _uploadSessionsRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<GetChunkUploadUrlRequest> _validator;

    public GetChunkUploadUrlHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<GetChunkUploadUrlHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository,
        IMultipartUploadSessionsRepository uploadSessionsRepository,
        ITransactionManager transactionManager,
        IValidator<GetChunkUploadUrlRequest> validator)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
        _uploadSessionsRepository = uploadSessionsRepository;
        _transactionManager = transactionManager;
        _validator = validator;
    }

    public async Task<Result<GetChunkUploadUrlResponse, Failure>> Handle(GetChunkUploadUrlRequest request,
        CancellationToken cancellationToken)
    {
        ValidationResult? validatorResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validatorResult.IsValid)
        {
            return validatorResult.ToErrors();
        }

        Result<MediaAsset, Error> mediaAssetResult = await _mediaAssetsRepository
            .GetById(request.MediaAssetId, cancellationToken);
        if (mediaAssetResult.IsFailure)
        {
            _logger.LogError("Media asset not found");
            return mediaAssetResult.Error.ToFailure();
        }

        MediaAsset mediaAsset = mediaAssetResult.Value;
        if (mediaAsset.Status != MediaStatus.UPLOADING)
        {
            _logger.LogError("Media asset has invalid status");
            return Errors.Validation.RecordIsInvalid("media_asset_status").ToFailure();
        }

        Result<MultipartUploadSession, Error> sessionResult = await _uploadSessionsRepository
            .GetByMediaAssetIdAsync(request.MediaAssetId, cancellationToken);
        if (sessionResult.IsFailure)
            return sessionResult.Error.ToFailure();

        MultipartUploadSession session = sessionResult.Value;
        if (!string.Equals(session.UploadId, request.UploadId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Multipart upload session does not match media asset {MediaAssetId}",
                request.MediaAssetId);
            return Errors.Validation.RecordIsInvalid("multipart_upload_session").ToFailure();
        }

        DateTime now = DateTime.UtcNow;
        if (session.ExpiresAt <= now)
        {
            return Error.Validation(
                "multipart.upload.expired",
                "Multipart upload session has expired").ToFailure();
        }

        if (session.Status != MultipartUploadStatus.ACTIVE)
        {
            return Error.Conflict(
                "multipart.upload.not_active",
                $"Multipart upload is in {session.Status} status").ToFailure();
        }

        StorageKey storageKey = mediaAsset.UploadKey;
        Result<ListMultipartUploadsResponse, Error> uploadsResult =
            await _fileStorageProvider.FileListMultipartUploadAsync(storageKey, cancellationToken);
        if (uploadsResult.IsFailure)
            return uploadsResult.Error.ToFailure();

        bool existsInStorage = (uploadsResult.Value.MultipartUploads ?? []).Any(upload =>
            string.Equals(upload.UploadId, request.UploadId, StringComparison.Ordinal)
            && string.Equals(upload.Key, storageKey.Value, StringComparison.Ordinal));
        if (!existsInStorage)
        {
            UnitResult<Error> failSessionResult =
                session.Fail("Multipart upload no longer exists in object storage");
            if (failSessionResult.IsFailure)
                return failSessionResult.Error.ToFailure();

            UnitResult<Error> failMediaResult = mediaAsset.MarkFailed();
            if (failMediaResult.IsFailure)
                return failMediaResult.Error.ToFailure();

            UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
            if (saveResult.IsFailure)
                return saveResult.Error.ToFailure();

            return Error.Validation(
                "multipart.upload.not_found",
                "Multipart upload no longer exists in object storage").ToFailure();
        }

        Result<string, Error> uploadUrlsAsync =
            await _fileStorageProvider.GenerateChunkUploadUrlAsync(
                storageKey,
                request.UploadId,
                request.PartNumber,
                cancellationToken);
        if (uploadUrlsAsync.IsFailure)
            return uploadUrlsAsync.Error.ToFailure();

        return new GetChunkUploadUrlResponse(uploadUrlsAsync.Value, request.PartNumber);
    }
}
