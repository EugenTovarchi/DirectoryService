using Dapper;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.Responses;
using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetDepsWithChildren;

public class GetDepartmentsWithChildrenHandler : IQueryHandler<PagedList<GetDepartmentsWithChildrenResponse>, GetDepartmentsWithChildrenQuery>
{
    private readonly INpgsqlConnectionFactory _connectionFactory;

    public GetDepartmentsWithChildrenHandler(INpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PagedList<GetDepartmentsWithChildrenResponse>> Handle(GetDepartmentsWithChildrenQuery query, CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        var parameters = new DynamicParameters();

        parameters.Add("page_size", query.Page);
        parameters.Add("offset", (query.Page - 1) * query.PageSize);

        parameters.Add("root_limit", query.RootLimit ?? 3);
        parameters.Add("child_limit", query.ChildLimit ?? 3);


        var direction = query.SortDirection?.ToLower() == "asc" ? "ASC"
            : "DESC";

        var orderByField = query.SortBy?.ToLower() switch
        {
            "name" => "name",
            "created_at" => "created_at",
            "updated_at" => "updated_at",
            "depth" => "depth",
            _ => "created_at"
        };

        var orderByClause = $"ORDER BY {orderByField} {direction}";

        var departments = await connection.QueryAsync<GetDepartmentsWithChildrenResponse>(
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
                {orderByClause}
                OFFSET @offset LIMIT @root_limit )

                SELECT *, 
                        (EXISTS (SELECT 1 from departments WHERE parent_id = roots.id OFFSET @child_limit LIMIT 1)) 
                                    AS has_more_children
                FROM roots 

                UNION ALL

                    SELECT ch.* , false AS has_more_children
                    FROM roots r
                        CROSS JOIN LATERAL( 
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
                            WHERE d.parent_id = r.id AND d.is_deleted = false
                            {orderByClause} 
                            LIMIT @child_limit) ch;
            """,
            parameters
            );

        return departments.ToPagedList(query.Page, query.PageSize);
    }
}
