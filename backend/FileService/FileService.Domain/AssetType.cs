namespace FileService.Domain;

public enum AssetType
{
    VIDEO,
    PHOTO,
    PREVIEW
}

public static class AsseTypeExtensions
{
    public static AssetType ToAssetType(this string value)
    {
        return value.ToLowerInvariant() switch
        {
            "video" => AssetType.VIDEO,
            "avatar" => AssetType.PHOTO,
            "preview" => AssetType.PREVIEW,
            _ => throw new ArgumentOutOfRangeException($"Invalid asset type: {value}", value)
        };
    }
}