using Dapper;
using DirectoryService.Application.Cache;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.Responses;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetDepsWithChildren;

public class
    GetDepartmentsWithChildrenHandler : IQueryHandler<List<DepartmentResponse>, GetDepartmentsWithChildrenQuery>
{
    private readonly INpgsqlConnectionFactory _connectionFactory;
    private readonly HybridCache _cache;
    private readonly CacheOptions _cacheOptions;
    private readonly ILogger<GetDepartmentsWithChildrenHandler> _logger;

    public GetDepartmentsWithChildrenHandler(
        INpgsqlConnectionFactory connectionFactory,
        HybridCache cache,
        IOptions<CacheOptions> cacheOptions,
        ILogger<GetDepartmentsWithChildrenHandler> logger)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    public async Task<List<DepartmentResponse>> Handle(GetDepartmentsWithChildrenQuery query,
        CancellationToken ct = default)
    {
        string cacheKey = BuildCacheKey(query);

        _logger.LogInformation("Getting data from cache...");

        List<DepartmentResponse> departments = await _cache.GetOrCreateAsync<List<DepartmentResponse>>(
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

        return departments;
    }

    private async Task<List<DepartmentResponse>> GetFromDatabase(GetDepartmentsWithChildrenQuery query,
            CancellationToken ct)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(ct);

            var parameters = new DynamicParameters();

            int page = query.Page > 0 ? query.Page : 1;
            int pageSize = query.PageSize > 0 ? query.PageSize : 20;

            parameters.Add("page_size", pageSize);
            parameters.Add("offset", (page - 1) * pageSize);

            parameters.Add("root_limit", query.RootLimit > 0 ? query.RootLimit.Value : 3);
            parameters.Add("child_limit", query.ChildLimit > 0 ? query.ChildLimit.Value : 3);

            const string sql =
                $"""
                 WITH roots AS(
                     SELECT d.id,
                            d.parent_id,
                            d.name,
                            d.identifier,
                            d.path,
                            d.depth,
                            d.created_at,
                            d.updated_at,
                            d.is_deleted
                     FROM departments d
                     WHERE d.parent_id IS NULL AND d.is_deleted = false
                     ORDER BY created_at ASC
                     OFFSET @offset LIMIT @root_limit)

                     SELECT *, 
                             (EXISTS (SELECT 1 from departments WHERE parent_id = roots.id OFFSET @child_limit LIMIT 1)) 
                                         AS has_more_children
                     FROM roots 

                     UNION ALL

                         SELECT ch.* , (EXISTS (SELECT 1 from departments WHERE parent_id = ch.id)) 
                                         AS has_more_children
                         FROM roots r
                             CROSS JOIN LATERAL (SELECT d.id,
                                                        d.parent_id,
                                                        d.name,
                                                        d.identifier,
                                                        d.path,
                                                        d.depth,
                                                        d.created_at,
                                                        d.updated_at,
                                                        d.is_deleted
                                                 FROM departments d
                                                 WHERE d.parent_id = r.id AND d.is_deleted = false
                                                 ORDER BY created_at ASC
                                                 LIMIT @child_limit) ch
                       ORDER BY created_at ASC;
                 """;

            var departmentsRaws = (await connection.QueryAsync<DepartmentResponse>(sql,
                parameters)).ToList();

            var departmentsDict = departmentsRaws.ToDictionary(x => x.Id);

            var roots = new List<DepartmentResponse>();

            foreach (var row in departmentsRaws)
            {
                if (row.ParentId.HasValue && departmentsDict.TryGetValue(row.ParentId.Value, out var parent))
                {
                    parent.Children.Add(departmentsDict[row.Id]);
                }
                else
                {
                    roots.Add(departmentsDict[row.Id]);
                }
            }

            return roots;
        }

    private string BuildCacheKey(GetDepartmentsWithChildrenQuery query)
    {
        int page = query.Page > 0 ? query.Page : 1;
        int pageSize = query.PageSize > 0 ? query.PageSize : 20;
        int rootLimit = query.RootLimit > 0 ? query.RootLimit.Value : 3;
        int childLimit = query.ChildLimit > 0 ? query.ChildLimit.Value : 3;

        return $"departments_with-children:" +
               $"rl={rootLimit}:" +
               $"cl={childLimit}:" +
               $"p={page}:" +
               $"ps={pageSize}";
    }
}