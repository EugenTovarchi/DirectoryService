using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
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

public sealed class StartMultipartUploadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/files/multipart/start",
            async Task<EndpointResult<StartMultipartUploadResponse>> (
                [FromBody] StartMultipartUploadRequest request,
                [FromServices] StartMultipartUploadHandler handler,
                CancellationToken cancellationToken) => await handler.Handle(request, cancellationToken));
    }
}

public sealed class StartMultipartUploadHandler
{
    private readonly ILogger<StartMultipartUploadHandler> _logger;
    private readonly IFileStorageProvider _fileStorageProvider;
    private readonly IChunkSizeCalculator _chunkSizeCalculator;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;

    public StartMultipartUploadHandler(
        IFileStorageProvider fileStorageProvider,
        IChunkSizeCalculator chunkSizeCalculator,
        ILogger<StartMultipartUploadHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository)
    {
        _fileStorageProvider = fileStorageProvider;
        _chunkSizeCalculator = chunkSizeCalculator;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
    }

    public async Task<Result<StartMultipartUploadResponse, Failure>> Handle(StartMultipartUploadRequest request,
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

        var mediaAssetResult = MediaAsset.CreateForUpload(mediaDataResult.Value, request.AssetType.ToAssetType());
        if (mediaAssetResult.IsFailure)
            return mediaAssetResult.Error.ToFailure();

        await _mediaAssetsRepository.AddAsync(mediaAssetResult.Value, cancellationToken);

        _logger.LogInformation("Media asset added to bd: {mediaAssetResult.Value.Id}", mediaAssetResult.Value.Id);

        var startUploadResult = await _fileStorageProvider.StartMultipartUploadAsync(
            mediaAssetResult.Value.Key,
            mediaAssetResult.Value.MediaData,
            cancellationToken);
        if (startUploadResult.IsFailure)
            return startUploadResult.Error.ToFailure();

        var chunkUploadUrlResult = await _fileStorageProvider.GenerateAllChunksUploadUrlsAsync(
            mediaAssetResult.Value.Key,
            startUploadResult.Value,
            chunkCalculatorResult.Value.TotalChunks,
            cancellationToken);
        if (chunkUploadUrlResult.IsFailure)
            return chunkUploadUrlResult.Error.ToFailure();

        mediaAssetResult.Value.MarkUploading();
        _logger.LogInformation("Media asset started uploading: {mediaAssetResult.Value.Key}",
            mediaAssetResult.Value.Key);

        return new StartMultipartUploadResponse(
            mediaAssetResult.Value.Id,
            startUploadResult.Value,
            chunkUploadUrlResult.Value,
            chunkCalculatorResult.Value.TotalChunks,
            chunkCalculatorResult.Value.ChunkSize);
    }
}