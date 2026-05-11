using AuthService.Contracts.Responses;
using AuthService.Core.Database;
using CSharpFunctionalExtensions;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SharedService.Core.Abstractions;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace AuthService.Core.Features.Queries.Users.GetUserById;

public sealed class GetUserByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/auth/users/{userId:guid}",
            async Task<EndpointResult<AuthUserResponse>> (
                [FromRoute] Guid userId,
                [FromServices] GetUserByIdHandler handler,
                CancellationToken cancellationToken) =>
            {
                var query = new GetUserByIdQuery(userId);
                return await handler.Handle(query, cancellationToken);
            });
    }
}

public record GetUserByIdQuery(Guid UserId) : IQuery;

public class GetUserByIdHandler : IQueryHandler<Result<AuthUserResponse, Failure>, GetUserByIdQuery>
{
    private readonly INpgsqlConnectionFactory _connectionFactory;

    public GetUserByIdHandler(INpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Result<AuthUserResponse, Failure>> Handle(
        GetUserByIdQuery query,
        CancellationToken ct = default)
    {
        if (query.UserId == Guid.Empty)
            return Errors.General.EmptyId(query.UserId).ToFailure();

        using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        const string sql = """
                           SELECT
                               id,
                               email,
                               username,
                               created_at AS CreatedAt,
                               updated_at AS UpdatedAt
                           FROM auth_users
                           WHERE id = @userId
                           """;

        var user = await connection.QuerySingleOrDefaultAsync<AuthUserResponse>(
            sql,
            new { userId = query.UserId });

        if (user is null)
            return Errors.General.NotFoundEntity("authUser").ToFailure();

        return user;
    }
}
