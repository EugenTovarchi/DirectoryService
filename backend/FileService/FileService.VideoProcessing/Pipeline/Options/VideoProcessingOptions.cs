namespace FileService.VideoProcessing.Pipeline.Options;

public sealed record VideoProcessingOptions
{
    public const string SECTION_NAME = "VideoProcessing";

    public string FfmpegPath { get; init; } = @"D:\Projects\DirectoryService\ffmpeg\bin\ffmpeg.exe";

    public string FfprobePath { get; init; } = @"D:\Projects\DirectoryService\ffmpeg\bin\ffprobe.exe";

    public bool UseHardwareAcceleration { get; init; }

    public string VideoEncoder { get; init; } = "libx264";

    public string VideoPreset { get; init; } = "medium";

    public int UploadDegreeOfParallelism { get; init; } = 3;

    public int MaxRetries { get; init; } = 3;

    public int RetryDelaySeconds { get; init; } = 10;

    // public string CronSchedule { get; set; } = "0/30 * * * * ?";
}