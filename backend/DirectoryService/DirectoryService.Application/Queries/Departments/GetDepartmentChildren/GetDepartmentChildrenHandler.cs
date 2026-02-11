using Dapper;
using DirectoryService.Application.Cache;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.Responses;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetDepartmentChildren;

public class GetDepartmentChildrenHandler : IQueryHandler<List<GetDepChildrenResponse>, GetDepartmentChildrenQuery>
{
    private readonly INpgsqlConnectionFactory _connectionFactory;
    private readonly HybridCache _cache;
    private readonly CacheOptions _cacheOptions;
    private readonly ILogger<GetDepartmentChildrenHandler> _logger;

    public GetDepartmentChildrenHandler(
        INpgsqlConnectionFactory connectionFactory,
        HybridCache cache,
        IOptions<CacheOptions> cacheOptions,
        ILogger<GetDepartmentChildrenHandler> logger)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    public async Task<List<GetDepChildrenResponse>> Handle(GetDepartmentChildrenQuery query,
        CancellationToken ct = default)
    {
        string cacheKey = BuildCacheKey(query);

        _logger.LogInformation("Getting data from cache...");

        List<GetDepChildrenResponse> children = await _cache.GetOrCreateAsync<List<GetDepChildrenResponse>>(
            key: cacheKey,
            factory: async _ =>
            {
                _logger.LogInformation("Cache is empty. Getting data from database...");
                return await GetFromDatabase(query, ct);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(_cacheOptions.DepartmentsCacheDurationMinutes)
            },
            cancellationToken: ct);

        return children;
    }

    private async Task<List<GetDepChildrenResponse>> GetFromDatabase(GetDepartmentChildrenQuery query,
        CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        var parameters = new DynamicParameters();

        int page = query.Request.Page > 0 ? query.Request.Page : 1;
        int pageSize = query.Request.PageSize > 0 ? query.Request.PageSize : 20;

        parameters.Add("page_size", pageSize);
        parameters.Add("offset", (page - 1) * pageSize);

        parameters.Add("parent_id", query.ParentId);

        var children = await connection.QueryAsync<GetDepChildrenResponse>(
            $"""
             SELECT d.id,
                    d.parent_id,
                    d.name,
                    d.identifier,
                    d.path,
                    d.depth,
                    d.created_at,
                    d.updated_at,
                    d.is_deleted,
                    (EXISTS (SELECT 1 from departments WHERE parent_id = d.id)) 
                                     AS has_more_children
              FROM departments d
              WHERE d.parent_id = @parent_id AND d.is_deleted = false
              ORDER BY created_at ASC
              OFFSET @offset LIMIT @page_size
             """,
            parameters);

        return children.ToList();
    }

    private string BuildCacheKey(GetDepartmentChildrenQuery query)
    {
        int page = query.Request.Page > 0 ? query.Request.Page : 1;
        int pageSize = query.Request.PageSize > 0 ? query.Request.PageSize : 20;

        return $"department={query.ParentId}:children:" +
               $"p={page}:" +
               $"ps={pageSize}";
    }
}