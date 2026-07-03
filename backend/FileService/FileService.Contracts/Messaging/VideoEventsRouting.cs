namespace FileService.Contracts.Messaging;

public static class VideoEventsRouting
{
    public const string DEPARTMENT_VIDEO_READY = "file.video.ready.department";

    public static string VideoReady(string targetEntityType)
    {
        return $"file.video.ready.{targetEntityType.ToLowerInvariant()}";
    }
}
