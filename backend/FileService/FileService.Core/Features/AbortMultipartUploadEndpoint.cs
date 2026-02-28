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

public sealed class AbortMultipartUploadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/multipart/abort",
            async Task<EndpointResult> (
                [FromBody] AbortMultipartUploadRequest request,
                [FromServices] AbortMultipartUploadHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(request, cancellationToken));
    }
}

public sealed class AbortMultipartUploadHandler
{
    private readonly ILogger<AbortMultipartUploadHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;

    public AbortMultipartUploadHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<AbortMultipartUploadHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
    }

    public async Task<UnitResult<Error>> Handle(AbortMultipartUploadRequest request,
        CancellationToken cancellationToken)
    {
        var mediaAssetResult = await _mediaAssetsRepository.GetBy(
            m => m.Id == request.MediaAssetId, cancellationToken);
        if (mediaAssetResult.IsFailure)
        {
            _logger.LogError("Media asset with id {id} was not found", request.MediaAssetId);
            return mediaAssetResult.Error;
        }

        MediaAsset mediaAsset = mediaAssetResult.Value;

        if (mediaAsset.Status != MediaStatus.UPLOADING)
        {
            return Error.Failure("media.upload.not_in_progress",
                $"Cannot abort upload for media in status: {mediaAsset.Status}");
        }

        UnitResult<Error> abortResult =
            await _fileStorageProvider.AbortMultipartUploadAsync(mediaAsset.Key, request.UploadId, cancellationToken);
        if (abortResult.IsFailure)
        {
            mediaAsset.MarkUploading();
            await _mediaAssetsRepository.SaveChangeAsync(cancellationToken);

            return abortResult.Error;
        }

        mediaAsset.MarkFailed();

        await _mediaAssetsRepository.SaveChangeAsync(cancellationToken);

        _logger.LogInformation("Uploading media: {id} was aborted", mediaAsset.Id);

        return UnitResult.Success<Error>();
    }
}