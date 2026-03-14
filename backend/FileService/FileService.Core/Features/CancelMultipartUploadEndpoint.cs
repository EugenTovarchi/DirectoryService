using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
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
                CancellationToken cancellationToken) => await handler.Handle(request, cancellationToken));
    }
}

public sealed class CancelMultipartUploadHandler
{
    private readonly ILogger<CancelMultipartUploadHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;

    public CancelMultipartUploadHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<CancelMultipartUploadHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
    }

    public async Task<UnitResult<Failure>> Handle(CancelMultipartUploadRequest request,
        CancellationToken cancellationToken)
    {
        var mediaAssetResult = await _mediaAssetsRepository.GetBy(
            m => m.Id == request.MediaAssetId, cancellationToken);
        if (mediaAssetResult.IsFailure)
        {
            _logger.LogError("Media asset with id {id} was not found", request.MediaAssetId);
            return mediaAssetResult.Error.ToFailure();
        }

        MediaAsset mediaAsset = mediaAssetResult.Value;

        if (mediaAsset.Status != MediaStatus.UPLOADING)
        {
            return Error.Failure("media.upload.not_in_progress",
                $"Cannot abort upload for media in status: {mediaAsset.Status}").ToFailure();
        }

        UnitResult<Error> abortResult =
            await _fileStorageProvider.AbortMultipartUploadAsync(mediaAsset.Key, request.UploadId, cancellationToken);
        if (abortResult.IsFailure)
        {
            mediaAsset.MarkUploading();
            await _mediaAssetsRepository.SaveChangeAsync(cancellationToken);

            return abortResult.Error.ToFailure();
        }

        await _mediaAssetsRepository.DeleteMediaAssetById(mediaAsset.Id, cancellationToken);

        _logger.LogInformation("Uploading media: {id} was aborted", request.MediaAssetId);

        return UnitResult.Success<Failure>();
    }
}