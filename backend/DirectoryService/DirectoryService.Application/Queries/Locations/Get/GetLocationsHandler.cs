using Dapper;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.Responses;
using DirectoryService.Core.Abstractions;
using System.Data;

namespace DirectoryService.Application.Queries.Locations.Get;

public class GetLocationsHandler : IQueryHandler<PagedList<GetLocationResponse>, GetLocationsQuery>
{
    private readonly INpgsqlConnectionFactory _connectionFactory;

    public GetLocationsHandler(INpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PagedList<GetLocationResponse>> Handle(GetLocationsQuery query, CancellationToken ct)
    {
        using var connection = await  _connectionFactory.CreateConnectionAsync(ct);
        var parameters = new DynamicParameters();
        var conditions = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("l.name ILIKE @search");
            parameters.Add("search", $"%{query.Search}%");
        }

        if (query.IsActive.HasValue)
        {
            var showDeleted = !query.IsActive.Value;
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

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(
                    " AND  ", conditions) : "";

        var direction = query.SortDirection?.ToLower() == "asc" ? "ASC"
            : "DESC";

        var orderByField = query.SortBy?.ToLower() switch
        {
            "name" => "name",
            "created at" => "created_at",
            "updated at" => "updated_at",
            "country" => "country",
            _ => "name"
			};

        var orderByClause = $"ORDER BY {orderByField} {direction}";

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
