using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
                CancellationToken cancellationToken) => await handler.Handle(request, cancellationToken));
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
    private readonly IFileReadDbContext _fileReadDbContext;
    private readonly IValidator<GetChunkUploadUrlRequest> _validator;

    public GetChunkUploadUrlHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<GetChunkUploadUrlHandler> logger,
        IFileReadDbContext fileReadDbContext,
        IValidator<GetChunkUploadUrlRequest> validator)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _fileReadDbContext = fileReadDbContext;
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

        MediaAsset? mediaAsset = await _fileReadDbContext.ReadMediaAssets
            .FirstOrDefaultAsync(m => m.Id == request.MediaAssetId, cancellationToken);
        if (mediaAsset == null)
        {
            _logger.LogError("Media asset not found");
            return Errors.General.NotFoundEntity("media_asset").ToFailure();
        }

        if (mediaAsset.Status != MediaStatus.UPLOADING)
        {
            _logger.LogError("Media asset has invalid status");
            return Errors.Validation.RecordIsInvalid("media_asset_status").ToFailure();
        }

        StorageKey storageKey = mediaAsset.Key;

        var checkUploadId = await _fileStorageProvider
            .FileListMultipartUploadAsync(storageKey, cancellationToken);

        if (checkUploadId.IsFailure)
            return checkUploadId.Error.ToFailure();

        var uploads = checkUploadId.Value.MultipartUploads ?? [];

        var existingUpload = uploads.FirstOrDefault(u =>
            u.UploadId == request.UploadId &&
            u.Key == storageKey.Value);

        if (existingUpload == null)
        {
            _logger.LogError("UploadId not found or does not match file");
            return Errors.Validation.RecordIsInvalid("existing_upload").ToFailure();
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