using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain.MediaProcessing.VO;

public sealed class VideoMetadata
{
    public TimeSpan Duration { get; }
    public int Width { get; }
    public int Height { get; }
    public bool HasAudio { get; }

    private VideoMetadata(
        TimeSpan duration,
        int width,
        int height,
        bool hasAudio)
    {
        Duration = duration;
        Width = width;
        Height = height;
        HasAudio = hasAudio;
    }

    private VideoMetadata() { }

    public static Result<VideoMetadata, Error> Create(
        TimeSpan duration,
        int width,
        int height,
        bool hasAudio = true)
    {
        if (duration <= TimeSpan.Zero)
            return Error.Validation("metadata.duration.invalid", "Duration must be > 0");

        if (width <= 0 || height <= 0)
            return Error.Validation("metadata.resolution.invalid", "Invalid resolution");

        return new VideoMetadata(duration, width, height, hasAudio);
    }
}
