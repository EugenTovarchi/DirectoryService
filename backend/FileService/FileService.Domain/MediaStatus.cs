using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain;

public enum MediaStatus
{
    UPLOADING,
    UPLOADED,
    DELETED,
    FAILED,
    READY
}

public static class MediaStatusExtensions
{
    public static Result<MediaStatus, Error> SwitchStatus(MediaStatus currentStatus, MediaStatus newStatus)
    {
        if (currentStatus == newStatus)
            return currentStatus;

        bool isAllowed = (currentStatus, newStatus) switch
        {
            (MediaStatus.UPLOADING, MediaStatus.UPLOADED) => true,
            (MediaStatus.UPLOADING, MediaStatus.FAILED) => true,
            (MediaStatus.UPLOADING, MediaStatus.DELETED) => true,

            (MediaStatus.UPLOADED, MediaStatus.READY) => true,
            (MediaStatus.UPLOADED, MediaStatus.FAILED) => true,
            (MediaStatus.UPLOADED, MediaStatus.DELETED) => true,

            (MediaStatus.READY, MediaStatus.DELETED) => true,

            (MediaStatus.FAILED, MediaStatus.DELETED) => true,

             _ => false
        };

        return isAllowed ? newStatus : Error.Failure($"media_status.invalid_switch",
            $"Cannot switch {currentStatus} to {newStatus}");
    }
}