using Dapper;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.Responses;
using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetTopByPositions;

public class GetTopByPositionsDepartmentsHandler(INpgsqlConnectionFactory connectionFactory)
    : IQueryHandler<List<GetTopDepartmentsResponse>, GetTopDepartmentsQuery>
{
    public async Task<List<GetTopDepartmentsResponse>> Handle(GetTopDepartmentsQuery query, CancellationToken ct)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(ct);
        var parameters = new DynamicParameters();

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
