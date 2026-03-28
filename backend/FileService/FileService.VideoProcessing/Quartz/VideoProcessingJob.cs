using FileService.Core;
using FileService.Domain;
using FileService.VideoProcessing.Pipeline;
using Microsoft.Extensions.Logging;
using Quartz;

namespace FileService.VideoProcessing.Quartz;

public class VideoProcessingJob : IJob
{
    private readonly VideoProcessingService _videoProcessingService;
    private readonly IMediaAssetsRepository _mediaAssetsRepository;
    private readonly ILogger<VideoProcessingJob> _logger;

    public VideoProcessingJob(
        VideoProcessingService videoProcessingService,
        IMediaAssetsRepository mediaAssetsRepository,
        ILogger<VideoProcessingJob> logger)
    {
        _videoProcessingService = videoProcessingService;
        _mediaAssetsRepository = mediaAssetsRepository;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var jobDataMap = context.MergedJobDataMap;

        if (!TryGetGuidFromJobData(jobDataMap, "VideoAssetId", out var videoAssetId))
        {
            _logger.LogError("Invalid or missing VideoAssetId in JobDataMap");
            return;
        }

        _logger.LogInformation("Starting processing for video {VideoAssetId}", videoAssetId);

        try
        {
            var mediaAssetResult = await _mediaAssetsRepository.GetBy(
                m => m.Id == videoAssetId, cancellationToken);

            if (mediaAssetResult.IsFailure)
            {
                _logger.LogError("Video asset {VideoAssetId} not found", videoAssetId);
                await context.Scheduler.DeleteJob(context.JobDetail.Key, cancellationToken);
                return;
            }

            var mediaAsset = mediaAssetResult.Value;

            if (mediaAsset.Status != MediaStatus.UPLOADED)
            {
                _logger.LogWarning(
                    "Video {VideoAssetId} is not in UPLOADED status (current: {Status}), skipping",
                    videoAssetId, mediaAsset.Status);

                await context.Scheduler.DeleteJob(context.JobDetail.Key, cancellationToken);
                return;
            }

            var result = await _videoProcessingService.ProcessVideoAsync(videoAssetId, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully processed video {VideoAssetId}", videoAssetId);
                await context.Scheduler.DeleteJob(context.JobDetail.Key, cancellationToken);
            }
            else
            {
                _logger.LogError("Failed to process video {VideoAssetId}: {Error}",
                    videoAssetId, result.Error.Message);

                // Сюда нужно будет добавить логику ретрая
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing video {VideoAssetId}", videoAssetId);
        }
    }

    private bool TryGetGuidFromJobData(JobDataMap jobDataMap, string key, out Guid result)
    {
        result = Guid.Empty;

        if (!jobDataMap.ContainsKey(key))
            return false;

        string? value = jobDataMap.GetString(key);
        if (string.IsNullOrEmpty(value))
            return false;

        return Guid.TryParse(value, out result);
    }
}