namespace FileService.VideoProcessing.Pipeline.Options;

public sealed record VideoProcessingOptions
{
    public const string SECTION_NAME = nameof(VideoProcessingOptions);

    public string FfmpegPath { get; init; } = "ffmpeg";

    public string FfprobePath { get; init; } = "ffprobe";

    public bool UseHardwareAcceleration { get; init; }

    public string VideoEncoder { get; init; } = "libx264";

    public string VideoPreset { get; init; } = "medium";

    public int UploadDegreeOfParallelism { get; init; } = 3;

    public int MaxRetries { get; init; } = 3;

    public int RetryDelaySeconds { get; init; } = 60;

    public int MaxConcurrentJobs { get; init; } = 2;

    public bool UsePersistentStore { get; init; } = true;

    public string QuartzTablePrefix { get; init; } = "quartz.qrtz_";

    public bool UseQuartzClustering { get; init; } = true;

    public int ClusterCheckinIntervalSeconds { get; init; } = 10;

    public int ClusterCheckinMisfireThresholdSeconds { get; init; } = 20;

    public int RecoveryScanIntervalSeconds { get; init; } = 60;

    public bool EnableRecoveryService { get; init; } = true;

    public bool EnableTempCleanupJob { get; init; } = true;

    public int TempDirectoryMaxAgeHours { get; init; } = 24;

    public int TempCleanupIntervalMinutes { get; init; } = 60;
}
