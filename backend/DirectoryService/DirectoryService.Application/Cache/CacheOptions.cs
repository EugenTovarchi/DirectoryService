namespace DirectoryService.Application.Cache;

public record CacheOptions
{
    public const string CACHE = "CacheOptions";

    public bool EnableCaching { get; init; }
    public short DefaultCacheDurationMinutes { get; init; }

    public short DefaultLocalCacheDurationMinutes { get; init; }

    public short DepartmentsCacheDurationMinutes { get; init; }
}