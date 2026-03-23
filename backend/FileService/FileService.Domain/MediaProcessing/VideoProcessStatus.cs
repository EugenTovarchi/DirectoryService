namespace FileService.Domain.MediaProcessing;

public enum VideoProcessStatus
{
    PENDING,
    RUNNING,
    SUCCEEDED,
    FAILED,
    CANCELED
}