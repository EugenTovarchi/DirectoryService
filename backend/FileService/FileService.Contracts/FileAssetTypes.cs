namespace FileService.Contracts;

public static class FileAssetTypes
{
    public const string Video = "video";
    public const string Avatar = "avatar";
    public const string Preview = "preview";

    private static readonly HashSet<string> _supportedValues = new(StringComparer.OrdinalIgnoreCase)
    {
        Video,
        Avatar,
        Preview
    };

    public static bool IsSupported(string? assetType)
    {
        return !string.IsNullOrWhiteSpace(assetType) && _supportedValues.Contains(assetType);
    }
}
