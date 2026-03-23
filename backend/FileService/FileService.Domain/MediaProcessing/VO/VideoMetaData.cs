using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain.MediaProcessing.VO;

public sealed class VideoMetadata
{
    public TimeSpan Duration { get; }
    public int Width { get; }
    public int Height { get; }
    public double FrameRate { get; }
    public string Codec { get; }
    public long Bitrate { get; }

    private VideoMetadata(
        TimeSpan duration,
        int width,
        int height,
        double frameRate,
        string codec,
        long bitrate)
    {
        Duration = duration;
        Width = width;
        Height = height;
        FrameRate = frameRate;
        Codec = codec;
        Bitrate = bitrate;
    }

    private VideoMetadata() { }

    public static Result<VideoMetadata, Error> Create(
        TimeSpan duration,
        int width,
        int height,
        double frameRate,
        string codec,
        long bitrate)
    {
        if (duration <= TimeSpan.Zero)
            return Error.Validation("metadata.duration.invalid", "Duration must be > 0");

        if (width <= 0 || height <= 0)
            return Error.Validation("metadata.resolution.invalid", "Invalid resolution");

        if (frameRate <= 0)
            return Error.Validation("metadata.fps.invalid", "FPS must be > 0");

        if (string.IsNullOrWhiteSpace(codec))
            return Error.Validation("metadata.codec.invalid", "Codec required");

        return new VideoMetadata(duration, width, height, frameRate, codec, bitrate);
    }
}