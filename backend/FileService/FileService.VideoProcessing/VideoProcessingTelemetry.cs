using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FileService.VideoProcessing;

public sealed class VideoProcessingTelemetry
{
    public const string METER_NAME = "FileService.VideoProcessing";
    public const string ACTIVITY_SOURCE_NAME = "FileService.VideoProcessing";

    private static readonly Meter _meter = new(METER_NAME);

    public static ActivitySource ActivitySource { get; } = new(ACTIVITY_SOURCE_NAME);

    public UpDownCounter<long> ActiveJobs { get; } =
        _meter.CreateUpDownCounter<long>("video_processing.active_jobs");

    public Counter<long> CompletedJobs { get; } =
        _meter.CreateCounter<long>("video_processing.completed_jobs");

    public Counter<long> FailedAttempts { get; } =
        _meter.CreateCounter<long>("video_processing.failed_attempts");

    public Counter<long> RetriedJobs { get; } =
        _meter.CreateCounter<long>("video_processing.retried_jobs");

    public Histogram<double> ProcessingDuration { get; } =
        _meter.CreateHistogram<double>("video_processing.duration", unit: "s");

    public Histogram<double> StepDuration { get; } =
        _meter.CreateHistogram<double>("video_processing.step.duration", unit: "s");
}
