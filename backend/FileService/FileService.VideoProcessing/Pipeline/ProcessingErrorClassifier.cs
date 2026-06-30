using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline;

public sealed class ProcessingErrorClassifier : IProcessingErrorClassifier
{
    private static readonly HashSet<string> _permanentFailureCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "process.failed",
        "ffprobe.invalid.output",
        "hls.processing.failed",
        "pipeline.handler.not.found",
        "preview.metadata.missing",
        "preview.workdir.missing",
        "workdir.directory.creation",
        "asset.invalid.status",
        "asset.invalid.status.transition",
        "video.status.invalid",
    };

    public bool IsCritical(Error error)
    {
        if (error.Type is ErrorType.VALIDATION or ErrorType.NOT_FOUND or ErrorType.CONFLICT)
            return true;

        return _permanentFailureCodes.Contains(error.Code);
    }
}
