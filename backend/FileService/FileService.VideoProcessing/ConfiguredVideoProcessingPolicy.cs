using FileService.Core.Abstractions;
using FileService.VideoProcessing.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace FileService.VideoProcessing;

public sealed class ConfiguredVideoProcessingPolicy(IOptions<VideoProcessingOptions> options)
    : IVideoProcessingPolicy
{
    public int MaxRetries => options.Value.MaxRetries;

    public TimeSpan GetRetryDelay(int retryCount)
    {
        int safeRetryCount = retryCount < 0 ? 0 : retryCount;

        int multiplier = safeRetryCount switch
        {
            0 => 1,
            1 => 2,
            2 => 4,
            _ => 8,
        };

        return TimeSpan.FromSeconds(options.Value.RetryDelaySeconds * multiplier);
    }
}
