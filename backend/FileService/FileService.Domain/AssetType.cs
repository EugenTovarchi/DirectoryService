namespace FileService.Domain;

public enum AssetType
{
    VIDEO,
    AVATAR,
    PREVIEW
}

public static class AsseTypeExtensions
{
    public static AssetType ToAssetType(this string value)
    {
        return value.ToLowerInvariant() switch
        {
            "video" => AssetType.VIDEO,
            "avatar" => AssetType.AVATAR,
            "preview" => AssetType.PREVIEW,
            _ => throw new ArgumentOutOfRangeException($"Invalid asset type: {value}", value)
        };
    }
}