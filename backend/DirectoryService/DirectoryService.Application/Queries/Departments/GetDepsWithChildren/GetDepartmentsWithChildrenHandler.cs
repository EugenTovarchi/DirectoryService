using Dapper;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.Responses;
using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetDepsWithChildren;

public class GetDepartmentsWithChildrenHandler : IQueryHandler<List<DepartmentResponse>, GetDepartmentsWithChildrenQuery>
{
    private readonly INpgsqlConnectionFactory _connectionFactory;

    public GetDepartmentsWithChildrenHandler(INpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<DepartmentResponse>> Handle(GetDepartmentsWithChildrenQuery query, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        var parameters = new DynamicParameters();

        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize > 0 ? query.PageSize : 20;

        parameters.Add("page_size", query.PageSize);
        parameters.Add("offset", (query.Page - 1) * query.PageSize);

        parameters.Add("root_limit", query.RootLimit > 0 ? query.RootLimit.Value : 3);
        parameters.Add("child_limit", query.RootLimit > 0 ? query.RootLimit.Value : 3);

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
}
