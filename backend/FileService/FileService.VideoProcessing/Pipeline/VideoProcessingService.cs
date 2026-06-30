using System.Diagnostics;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline;

public class VideoProcessingService : IVideoProcessingService
{
    private readonly ILogger<VideoProcessingService> _logger;
    private readonly IProcessingPipeline _processingPipeline;
    private readonly VideoProcessingTelemetry _telemetry;

    public VideoProcessingService(
        ILogger<VideoProcessingService> logger,
        IProcessingPipeline processingPipeline,
        VideoProcessingTelemetry telemetry)
    {
        _logger = logger;
        _processingPipeline = processingPipeline;
        _telemetry = telemetry;
    }

    public async Task<UnitResult<Error>> ProcessVideoAsync(
        Guid videoAssetId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        using Activity? activity = VideoProcessingTelemetry.ActivitySource.StartActivity("video.processing");
        activity?.SetTag("video.asset.id", videoAssetId);
        _telemetry.ActiveJobs.Add(1);

        _logger.LogInformation("Starting video processing for video asset {VideoAssetId}", videoAssetId);
        try
        {
            var pipelineResult = await _processingPipeline.ProcessAllStepsAsync(videoAssetId, cancellationToken);

            if (pipelineResult.IsSuccess)
            {
                _telemetry.CompletedJobs.Add(1);
                activity?.SetStatus(ActivityStatusCode.Ok);
                _logger.LogInformation(
                    "Completed video processing for video asset {VideoAssetId} in {ElapsedMilliseconds:0} ms",
                    videoAssetId, stopwatch.Elapsed.TotalMilliseconds);
            }
            else
            {
                _telemetry.FailedAttempts.Add(1);
                activity?.SetStatus(ActivityStatusCode.Error, pipelineResult.Error.Code);
            }

            return pipelineResult;
        }
        catch (Exception ex)
        {
            _telemetry.FailedAttempts.Add(1);
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            _logger.LogError(ex, "Unexpected error processing video {VideoAssetId}", videoAssetId);
            return UnitResult.Failure(Error.Failure(
                "video.processing.unexpected",
                $"Unexpected error: {ex.Message}"));
        }
        finally
        {
            stopwatch.Stop();
            _telemetry.ProcessingDuration.Record(stopwatch.Elapsed.TotalSeconds);
            _telemetry.ActiveJobs.Add(-1);
        }
    }
}
