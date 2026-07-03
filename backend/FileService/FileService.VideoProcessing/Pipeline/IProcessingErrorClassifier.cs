using SharedService.SharedKernel;

namespace FileService.VideoProcessing.Pipeline;

public interface IProcessingErrorClassifier
{
    bool IsCritical(Error error);
}
