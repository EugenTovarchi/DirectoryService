using Dapper;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.Responses;
using DirectoryService.Core.Abstractions;

namespace DirectoryService.Application.Queries.Departments.GetDepartmentChildren;

public class GetDepartmentChildrenHandler : IQueryHandler<List<GetDepChildrenResponse>, GetDepartmentChildrenQuery>
{
    private readonly INpgsqlConnectionFactory _connectionFactory;

    public GetDepartmentChildrenHandler(INpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<GetDepChildrenResponse>> Handle(GetDepartmentChildrenQuery query,
        CancellationToken ct = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        var parameters = new DynamicParameters();

        var page = query.Request.Page > 0 ? query.Request.Page : 1;
        var pageSize = query.Request.PageSize > 0 ? query.Request.PageSize : 20;

        parameters.Add("page_size", pageSize);
        parameters.Add("offset", (page - 1) * query.Request.PageSize);

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
}