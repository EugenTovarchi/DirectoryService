using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Core.FilesStorage;
using FileService.Domain.Assets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace FileService.Core.Features;

public sealed class CompleteMultipartUploadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/multipart/end",
            async Task<EndpointResult> (
                [FromBody] CompleteMultipartUploadRequest request,
                [FromServices] CompleteMultipartUploadHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(request, cancellationToken));
    }
}

public sealed class CompleteMultipartUploadHandler
{
    private readonly ILogger<CompleteMultipartUploadHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;

    public CompleteMultipartUploadHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<CompleteMultipartUploadHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
    }

    public async Task<UnitResult<Error>> Handle(CompleteMultipartUploadRequest request,
        CancellationToken cancellationToken)
    {
        var mediaAssetResult = await _mediaAssetsRepository.GetBy(
            m => m.Id == request.MediaAssetId, cancellationToken);
        if (mediaAssetResult.IsFailure)
            return mediaAssetResult.Error;

        MediaAsset mediaAsset = mediaAssetResult.Value;

        if (mediaAsset.MediaData.ExpectedChunkCount != request.PartETags.Count)
            return Errors.General.ValueIsInvalid("Count of expected chunks are not equal to part etags count!");

        Result<string, Error> completeResult =
            await _fileStorageProvider.CompleteMultipartUploadAsync(mediaAsset.Key, request.UploadId, request.PartETags,
                cancellationToken);
        if (completeResult.IsFailure)
        {
            mediaAsset.MarkFailed();
            await _mediaAssetsRepository.SaveChangeAsync(cancellationToken);

            return completeResult.Error;
        }

        mediaAsset.MarkUploaded();

        await _mediaAssetsRepository.SaveChangeAsync(cancellationToken);

        _logger.LogInformation("Success complete to upload of {id}", mediaAsset.Id);

        return UnitResult.Success<Error>();
    }
}