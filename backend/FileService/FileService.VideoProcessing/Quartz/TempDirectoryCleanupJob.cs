using FileService.VideoProcessing.Pipeline.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace FileService.VideoProcessing.Quartz;

[DisallowConcurrentExecution]
public sealed class TempDirectoryCleanupJob(
    IOptions<VideoProcessingOptions> options,
    ILogger<TempDirectoryCleanupJob> logger)
    : IJob
{
    public const string JOB_NAME = "VideoProcessingTempCleanup";

    public Task Execute(IJobExecutionContext context)
    {
        if (!options.Value.EnableTempCleanupJob)
            return Task.CompletedTask;

        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        DateTime cutoffUtc = DateTime.UtcNow.AddHours(-options.Value.TempDirectoryMaxAgeHours);
        int deletedCount = 0;

        foreach (string candidate in Directory.EnumerateDirectories(tempRoot, "video-processing*"))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                string fullPath = Path.GetFullPath(candidate);
                if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "Skipping temp cleanup candidate outside temp root: {DirectoryPath}",
                        fullPath);
                    continue;
                }

                var directory = new DirectoryInfo(fullPath);
                if (directory.LastWriteTimeUtc > cutoffUtc)
                    continue;

                directory.Delete(recursive: true);
                deletedCount++;
            }
            catch (DirectoryNotFoundException)
            {
                // Another cleanup process already removed it.
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete stale video processing directory {DirectoryPath}", candidate);
            }
        }

        if (deletedCount > 0)
        {
            logger.LogInformation("Deleted {DeletedDirectoryCount} stale video processing directories", deletedCount);
        }
        else
        {
            logger.LogDebug("Video temp cleanup found no stale directories");
        }

        return Task.CompletedTask;
    }
}
