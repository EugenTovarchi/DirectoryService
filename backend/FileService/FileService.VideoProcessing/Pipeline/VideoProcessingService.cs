using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline;

public class VideoProcessingService : IVideoProcessingService
{
    private readonly ILogger<VideoProcessingService> _logger;
    private readonly IProcessingPipeline _processingPipeline;

    public VideoProcessingService(
        ILogger<VideoProcessingService> logger,
        IProcessingPipeline processingPipeline)
    {
        _logger = logger;
        _processingPipeline = processingPipeline;
    }

    public async Task<UnitResult<Error>> ProcessVideoAsync(
        Guid videoAssetId,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("Starting processing video for video asset {VideoAssetId}", videoAssetId);
        try
        {
            var pipelineResult = await _processingPipeline.ProcessAllStepsAsync(videoAssetId, cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            if (pipelineResult.IsSuccess)
            {
                _logger.LogInformation("Successfully processed video {VideoAssetId} in {Duration}ms",
                    videoAssetId, duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogError("Failed to process video {VideoAssetId}: {Error}",
                    videoAssetId, pipelineResult.Error.Message);
            }

            return pipelineResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing video {VideoAssetId}", videoAssetId);
            return UnitResult.Failure(Error.Failure(
                "video.processing.unexpected",
                $"Unexpected error: {ex.Message}"));
        }
    }
}