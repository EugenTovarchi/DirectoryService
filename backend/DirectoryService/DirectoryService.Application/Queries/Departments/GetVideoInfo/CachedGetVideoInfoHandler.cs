using CSharpFunctionalExtensions;
using DirectoryService.Application.Cache;
using DirectoryService.Contracts.Responses;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace DirectoryService.Application.Queries.Departments.GetVideoInfo;

public sealed class CachedGetVideoInfoHandler : IGetVideoInfoHandler
{
    private const string DEPARTMENTS_CACHE_TAG = "departments";

    private readonly IGetVideoInfoHandler _inner;
    private readonly HybridCache _cache;
    private readonly CacheOptions _cacheOptions;

    public CachedGetVideoInfoHandler(
        IGetVideoInfoHandler inner,
        HybridCache cache,
        IOptions<CacheOptions> cacheOptions)
    {
        _inner = inner;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
    }

    public async Task<Result<GetVideoInfoResponse, Failure>> Handle(
        GetVideoInfoQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            GetVideoInfoResponse response = await _cache.GetOrCreateAsync(
                key: BuildCacheKey(query.DepartmentId),
                factory: async factoryCancellationToken =>
                {
                    Result<GetVideoInfoResponse, Failure> result =
                        await _inner.Handle(query, factoryCancellationToken);

                    if (result.IsFailure || !CanBeCached(result.Value))
                    {
                        throw new NonCacheableResultException(result);
                    }

                    return result.Value;
                },
                tags: [DEPARTMENTS_CACHE_TAG],
                options: new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(
                        _cacheOptions.DepartmentsCacheDurationMinutes),
                    LocalCacheExpiration = TimeSpan.FromMinutes(
                        _cacheOptions.DefaultLocalCacheDurationMinutes)
                },
                cancellationToken: cancellationToken);

            return response;
        }
        catch (NonCacheableResultException exception)
        {
            return exception.Result;
        }
    }

    private static string BuildCacheKey(Guid departmentId) =>
        $"department:{departmentId}:video-info";

    private static bool CanBeCached(GetVideoInfoResponse response) =>
        string.Equals(response.Status, "ready", StringComparison.OrdinalIgnoreCase) &&
        response.Duration.HasValue &&
        response.Width.HasValue &&
        response.Height.HasValue &&
        response.HasAudio.HasValue;

#pragma warning disable CA1032, CA1064, S3871

    // Internal control-flow signal: HybridCache skips writes when its factory throws.
    private sealed class NonCacheableResultException(
        Result<GetVideoInfoResponse, Failure> result) : Exception
    {
        public Result<GetVideoInfoResponse, Failure> Result { get; } = result;
    }
#pragma warning restore CA1032, CA1064, S3871
}
