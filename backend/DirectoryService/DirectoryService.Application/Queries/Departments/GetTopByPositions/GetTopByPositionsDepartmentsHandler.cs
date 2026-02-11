using Dapper;
using DirectoryService.Application.Cache;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.Responses;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetTopByPositions;

public class
    GetTopByPositionsDepartmentsHandler : IQueryHandler<List<GetTopDepartmentsResponse>, GetTopDepartmentsQuery>
{
    private readonly INpgsqlConnectionFactory _connectionFactory;
    private readonly HybridCache _cache;
    private readonly CacheOptions _cacheOptions;
    private readonly ILogger<GetTopByPositionsDepartmentsHandler> _logger;

    public GetTopByPositionsDepartmentsHandler(
        INpgsqlConnectionFactory connectionFactory,
        HybridCache cache,
        IOptions<CacheOptions> cacheOptions,
        ILogger<GetTopByPositionsDepartmentsHandler> logger)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    public async Task<List<GetTopDepartmentsResponse>> Handle(GetTopDepartmentsQuery query, CancellationToken ct)
    {
        string cacheKey = $"top_5_departments_by_positions: {query.SortDirection ?? "desc"}";

        _logger.LogInformation("Getting departments by positions from cache");

        List<GetTopDepartmentsResponse> departments = await _cache.GetOrCreateAsync<List<GetTopDepartmentsResponse>>(
            key: cacheKey,
            factory: async _ =>
            {
                _logger.LogInformation("Cache is empty. Getting departments by positions from database...");
                return await GetFromDatabase(query, ct);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(_cacheOptions.DepartmentsCacheDurationMinutes)
            },
            cancellationToken: ct);

        return departments;
    }

    private async Task<List<GetTopDepartmentsResponse>> GetFromDatabase(GetTopDepartmentsQuery query,
        CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        string direction = query.SortDirection?.ToLower() == "asc" ? "ASC" : "DESC";

        var departments = await connection.QueryAsync<GetTopDepartmentsResponse>(
            $"""
             WITH department_stats AS (
             SELECT 
                 d.id,
                 d.name,
                 d.identifier,
                 d.path,
                 d.depth,
                 parent_department.name as parent_name,
                 d.parent_id,
                 d.created_at,
                 d.updated_at as last_updated_at,
                 COUNT(dp.position_id) as position_count
                 FROM departments d
                 LEFT JOIN department_positions dp ON d.id = dp.department_id
                 LEFT JOIN departments parent_department  ON d.parent_id = parent_department.id
                 WHERE d.is_deleted = false
                 GROUP BY    d.id,
                             d.name,
                             d.identifier,
                             d.path,
                             d.depth,
                             parent_department.name,
                             d.parent_id,
                             d.created_at,
                             d.updated_at 
                 )
             SELECT
                 ds.name,
                 ds.identifier,
                 ds.path,
                 ds.depth,
                 COALESCE (ds.parent_name, '') as parent_name, 
                 ds.parent_id,
                 ds.created_at,
                 ds.last_updated_at,
                 ds.position_count,
                 MAX (ds.position_count) OVER () as max_position_count
             FROM department_stats ds
             ORDER BY ds.position_count  {direction}
             LIMIT 5     
             """);

        return departments.ToList();
    }
}