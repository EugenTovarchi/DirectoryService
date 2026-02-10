using System.Data;
using Dapper;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.Responses;
using SharedService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Locations.Get;

public class GetLocationsHandler(INpgsqlConnectionFactory connectionFactory)
    : IQueryHandler<PagedList<GetLocationResponse>, GetLocationsQuery>
{
    public async Task<PagedList<GetLocationResponse>> Handle(GetLocationsQuery query, CancellationToken ct)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(ct);
        var parameters = new DynamicParameters();
        var conditions = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("l.name ILIKE @search");
            parameters.Add("search", $"%{query.Search}%");
        }

        if (query.IsActive.HasValue)
        {
            bool showDeleted = !query.IsActive.Value;
            conditions.Add("l.is_deleted = @showDeleted");
            parameters.Add("showDeleted", showDeleted, DbType.Boolean);
        }

        if (query.DepartmentsIds is not null && query.DepartmentsIds.Count != 0)
        {
            conditions.Add("""
                 EXISTS (
                    SELECT 1 
                    FROM department_locations dl 
                    WHERE dl.location_id = l.id 
                    AND dl.department_id = ANY(@departmentIds))
                """);

            parameters.Add("departmentIds", query.DepartmentsIds);
        }

        parameters.Add("page_size", query.PageSize);
        parameters.Add("offset", (query.Page - 1) * query.PageSize);

        string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(
                    " AND  ", conditions) : string.Empty;

        string direction = query.SortDirection?.ToLower() == "asc" ? "ASC"
            : "DESC";

        string orderByField = query.SortBy?.ToLower() switch
        {
            "name" => "name",
            "created at" => "created_at",
            "updated at" => "updated_at",
            "country" => "country",
            _ => "name"
            };

        string orderByClause = $"ORDER BY {orderByField} {direction}";

        var locations = await connection.QueryAsync<GetLocationResponse>(
            $"""
            SELECT
                name,
                country,
                city,
                street,
                house, 
                flat,
                time_zone,
                created_at,
                updated_at
            FROM locations l
            {whereClause}
            {orderByClause}
            LIMIT @page_size OFFSET @offset
            """,
            param: parameters);

        return locations.ToPagedList(query.Page, query.PageSize);
    }
}
