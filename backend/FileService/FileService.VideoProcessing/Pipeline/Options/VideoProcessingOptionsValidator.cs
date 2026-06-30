using Microsoft.Extensions.Options;

namespace FileService.VideoProcessing.Pipeline.Options;

public sealed class VideoProcessingOptionsValidator : IValidateOptions<VideoProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, VideoProcessingOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.FfmpegPath))
            failures.Add("VideoProcessingOptions:FfmpegPath is required.");

        if (string.IsNullOrWhiteSpace(options.FfprobePath))
            failures.Add("VideoProcessingOptions:FfprobePath is required.");

        if (string.IsNullOrWhiteSpace(options.VideoEncoder))
            failures.Add("VideoProcessingOptions:VideoEncoder is required.");

        if (string.IsNullOrWhiteSpace(options.VideoPreset))
            failures.Add("VideoProcessingOptions:VideoPreset is required.");

        if (options.UploadDegreeOfParallelism <= 0)
            failures.Add("VideoProcessingOptions:UploadDegreeOfParallelism must be greater than zero.");

        if (options.MaxRetries < 0)
            failures.Add("VideoProcessingOptions:MaxRetries must not be negative.");

        if (options.RetryDelaySeconds <= 0)
            failures.Add("VideoProcessingOptions:RetryDelaySeconds must be greater than zero.");

        if (options.MaxConcurrentJobs <= 0)
            failures.Add("VideoProcessingOptions:MaxConcurrentJobs must be greater than zero.");

        if (options.UsePersistentStore && string.IsNullOrWhiteSpace(options.QuartzTablePrefix))
            failures.Add("VideoProcessingOptions:QuartzTablePrefix is required for persistent Quartz store.");

        if (options.UseQuartzClustering && options.ClusterCheckinIntervalSeconds <= 0)
            failures.Add("VideoProcessingOptions:ClusterCheckinIntervalSeconds must be greater than zero.");

        if (options.UseQuartzClustering && options.ClusterCheckinMisfireThresholdSeconds <= 0)
        {
            failures.Add(
                "VideoProcessingOptions:ClusterCheckinMisfireThresholdSeconds must be greater than zero.");
        }

        if (options.EnableRecoveryService && options.RecoveryScanIntervalSeconds <= 0)
            failures.Add("VideoProcessingOptions:RecoveryScanIntervalSeconds must be greater than zero.");

        if (options.EnableTempCleanupJob && options.TempDirectoryMaxAgeHours <= 0)
            failures.Add("VideoProcessingOptions:TempDirectoryMaxAgeHours must be greater than zero.");

        if (options.EnableTempCleanupJob && options.TempCleanupIntervalMinutes <= 0)
            failures.Add("VideoProcessingOptions:TempCleanupIntervalMinutes must be greater than zero.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
