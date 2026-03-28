using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Core.Abstractions;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing;
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
    private readonly IVideoProcessingScheduler _videoProcessingScheduler;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;
    private readonly IVideoProcessesRepository _videoProcessesRepository;
    private readonly ITransactionManager _transactionManager;

    public CompleteMultipartUploadHandler(
        IFileStorageProvider fileStorageProvider,
        ILogger<CompleteMultipartUploadHandler> logger,
        IMediaAssetsRepository mediaAssetsRepository,
        ITransactionManager transactionManager,
        IVideoProcessingScheduler videoProcessingScheduler,
        IVideoProcessesRepository videoProcessesRepository)
    {
        _fileStorageProvider = fileStorageProvider;
        _logger = logger;
        _mediaAssetsRepository = mediaAssetsRepository;
        _transactionManager = transactionManager;
        _videoProcessingScheduler = videoProcessingScheduler;
        _videoProcessesRepository = videoProcessesRepository;
    }

    public async Task<UnitResult<Failure>> Handle(CompleteMultipartUploadRequest request,
        CancellationToken cancellationToken)
    {
        var mediaAssetResult = await _mediaAssetsRepository.GetBy(m => m.Id == request.MediaAssetId, cancellationToken);
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
            var saveMediaAssetResult = await _transactionManager.SaveChangeAsync(cancellationToken);
            if (saveMediaAssetResult.IsFailure)
            {
                _logger.LogError("Error when try to save media asset!");
                saveMediaAssetResult.Error.ToFailure();
            }

            return completeResult.Error.ToFailure();
        }

        var markUploadedResult = mediaAsset.MarkUploaded();
        if (markUploadedResult.IsFailure)
        {
            _logger.LogError("Failed to mark media asset as UPLOADED: {Error}", markUploadedResult.Error.Message);
            return markUploadedResult.Error.ToFailure();
        }

        var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
        {
            _logger.LogError("Error when try to save changes!");
        }

        if (mediaAsset.RequiresProcessing() && mediaAsset.AssetType == AssetType.VIDEO)
        {
            var createVideoProcessResult = VideoProcess.Create(mediaAsset.Id, mediaAsset.UploadKey);
            if (createVideoProcessResult.IsFailure)
            {
                _logger.LogError("Failed to create video process: {Error} for media asset: {id}",
                    createVideoProcessResult.Error.Message, mediaAsset.Id);

                return createVideoProcessResult.Error.ToFailure();
            }

            var videoProcess = createVideoProcessResult.Value;

            _videoProcessesRepository.Add(videoProcess);

            var saveVideoProcessResult = await _transactionManager.SaveChangeAsync(cancellationToken);
            if (saveVideoProcessResult.IsFailure)
            {
                _logger.LogError("Error when try to save video process!");
                saveVideoProcessResult.Error.ToFailure();
            }

            var scheduleResult = await _videoProcessingScheduler.ScheduleProcessingAsync(videoProcess.VideoAssetId,
                cancellationToken);
            if (scheduleResult.IsFailure)
            {
                _logger.LogError("Schedule processing failed: {Error} for media asset: {id} ",
                    scheduleResult.Error.Message, videoProcess.VideoAssetId);
            }
            else
            {
                _logger.LogInformation(
                    "Successfully scheduled video processing for {VideoAssetId}",
                    videoProcess.VideoAssetId);
            }
        }

        _logger.LogInformation("Success complete to upload of {id}", mediaAsset.Id);

        return UnitResult.Success<Failure>();
    }
}