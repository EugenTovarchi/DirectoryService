using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using AuthService.Core.Authorization;
using AuthService.Core.Database;
using AuthService.Core.Extensions;
using AuthService.Core.Failures;
using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using Dapper;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SharedService.Core.Abstractions;
using SharedService.Core.Validation;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace AuthService.Core.Features.Queries.Users.GetUsers;

public sealed class GetUsersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/users",
            async Task<EndpointResult<PagedList<CompanyUserResponse>>> (
                [AsParameters] GetUsersRequest request,
                HttpContext httpContext,
                [FromServices] GetUsersHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid requestedByUserId = httpContext.User.GetUserId();
                GetUsersQuery query = new(requestedByUserId, request.Page, request.PageSize);

                return await handler.Handle(query, cancellationToken);
            })
            .RequireAuthorization(AuthPolicies.USERS_MANAGE);
    }
}

public sealed record GetUsersQuery(
    Guid RequestedByUserId,
    int Page,
    int PageSize) : IQuery;

public sealed class GetUsersValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersValidator()
    {
        RuleFor(query => query.RequestedByUserId)
            .NotEmpty();

        RuleFor(query => query.Page)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}

public sealed class GetUsersHandler : IQueryHandler<Result<PagedList<CompanyUserResponse>, Failure>, GetUsersQuery>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INpgsqlConnectionFactory _connectionFactory;
    private readonly IValidator<GetUsersQuery> _validator;

    public GetUsersHandler(
        UserManager<ApplicationUser> userManager,
        INpgsqlConnectionFactory connectionFactory,
        IValidator<GetUsersQuery> validator)
    {
        _userManager = userManager;
        _connectionFactory = connectionFactory;
        _validator = validator;
    }

    public async Task<Result<PagedList<CompanyUserResponse>, Failure>> Handle(
        GetUsersQuery query,
        CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        ApplicationUser? requestedByUser = await _userManager.FindByIdAsync(query.RequestedByUserId.ToString());
        if (requestedByUser is null || !requestedByUser.IsActive)
            return AuthFailures.InvalidAuthenticatedUser();

        bool requestedBySystemAdmin = await _userManager.IsInRoleAsync(requestedByUser, AuthRoles.SYSTEM_ADMIN);
        if (!requestedBySystemAdmin && requestedByUser.CurrentCompanyId is null)
            return UserManagementFailures.InvalidCompanyContextForList();

        using var connection = await _connectionFactory.CreateConnectionAsync(ct);

        DynamicParameters parameters = new();
        List<string> conditions = [];
        parameters.Add("page_size", query.PageSize);
        parameters.Add("offset", (query.Page - 1) * query.PageSize);

        if (!requestedBySystemAdmin)
        {
            Guid companyId = requestedByUser.CurrentCompanyId!.Value;
            conditions.Add("u.current_company_id = @company_id");
            parameters.Add("company_id", companyId);
        }

        string whereClause = conditions.Count > 0
            ? $"WHERE {string.Join(" AND ", conditions)}"
            : string.Empty;

        long totalCount = await connection.ExecuteScalarAsync<long>(
            $"""
            SELECT COUNT(*)
            FROM identity_users u
            {whereClause}
            """,
            parameters);

        IEnumerable<CompanyUserRow> users = await connection.QueryAsync<CompanyUserRow>(
            $"""
            SELECT
                u.id AS user_id,
                u.email,
                u.user_name AS username,
                u.display_name,
                u.current_company_id AS company_id,
                u.is_active,
                COALESCE(
                    ARRAY_AGG(r.name ORDER BY r.name) FILTER (WHERE r.name IS NOT NULL),
                    ARRAY[]::text[]
                ) AS roles
            FROM identity_users u
            LEFT JOIN identity_user_roles ur ON ur.user_id = u.id
            LEFT JOIN identity_roles r ON r.id = ur.role_id
            {whereClause}
            GROUP BY
                u.id,
                u.email,
                u.user_name,
                u.display_name,
                u.current_company_id,
                u.is_active
            ORDER BY u.email
            LIMIT @page_size OFFSET @offset
            """,
            parameters);

        return new PagedList<CompanyUserResponse>
        {
            Items = users
                .Select(user => new CompanyUserResponse(
                    user.UserId,
                    user.Email,
                    user.Username,
                    user.DisplayName,
                    user.CompanyId,
                    user.IsActive,
                    user.Roles.Cast<string>().ToArray()))
                .ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
        };
    }

    private sealed record CompanyUserRow(
        Guid UserId,
        string Email,
        string Username,
        string? DisplayName,
        Guid? CompanyId,
        bool IsActive,
        Array Roles);
}
