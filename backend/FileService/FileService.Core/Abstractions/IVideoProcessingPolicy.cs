namespace FileService.Core.Abstractions;

public interface IVideoProcessingPolicy
{
    int MaxRetries { get; }

    TimeSpan GetRetryDelay(int retryCount);
}
