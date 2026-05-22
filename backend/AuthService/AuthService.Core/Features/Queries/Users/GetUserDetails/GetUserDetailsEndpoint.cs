using System.Data;
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

namespace AuthService.Core.Features.Queries.Users.GetUserDetails;

public sealed class GetUserDetailsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/users/{userId:guid}",
            async Task<EndpointResult<CompanyUserDetailsResponse>> (
                Guid userId,
                HttpContext httpContext,
                [FromServices] GetUserDetailsHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid requestedByUserId = httpContext.User.GetUserId();
                GetUserDetailsQuery query = new(requestedByUserId, userId);

                return await handler.Handle(query, cancellationToken);
            })
            .RequireAuthorization(AuthPolicies.USERS_MANAGE);
    }
}

public sealed record GetUserDetailsQuery(
    Guid RequestedByUserId,
    Guid UserId) : IQuery;

public sealed class GetUserDetailsValidator : AbstractValidator<GetUserDetailsQuery>
{
    public GetUserDetailsValidator()
    {
        RuleFor(query => query.RequestedByUserId)
            .NotEmpty();

        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}

public sealed class GetUserDetailsHandler : IQueryHandler<Result<CompanyUserDetailsResponse, Failure>, GetUserDetailsQuery>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INpgsqlConnectionFactory _connectionFactory;
    private readonly IValidator<GetUserDetailsQuery> _validator;

    public GetUserDetailsHandler(
        UserManager<ApplicationUser> userManager,
        INpgsqlConnectionFactory connectionFactory,
        IValidator<GetUserDetailsQuery> validator)
    {
        _userManager = userManager;
        _connectionFactory = connectionFactory;
        _validator = validator;
    }

    public async Task<Result<CompanyUserDetailsResponse, Failure>> Handle(
        GetUserDetailsQuery query,
        CancellationToken ct = default)
    {
        FluentValidation.Results.ValidationResult validationResult = await _validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        ApplicationUser? requestedByUser = await _userManager.FindByIdAsync(query.RequestedByUserId.ToString());
        if (requestedByUser is null || !requestedByUser.IsActive)
            return AuthFailures.InvalidAuthenticatedUser();

        bool requestedBySystemAdmin = await _userManager.IsInRoleAsync(requestedByUser, AuthRoles.SYSTEM_ADMIN);
        if (!requestedBySystemAdmin && requestedByUser.CurrentCompanyId is null)
            return UserManagementFailures.InvalidCompanyContextForList();

        using IDbConnection connection = await _connectionFactory.CreateConnectionAsync(ct);

        DynamicParameters parameters = new();
        List<string> conditions = ["u.id = @user_id"];
        parameters.Add("user_id", query.UserId);

        if (!requestedBySystemAdmin)
        {
            Guid companyId = requestedByUser.CurrentCompanyId!.Value;
            conditions.Add("u.current_company_id = @company_id");
            parameters.Add("company_id", companyId);
        }

        string whereClause = $"WHERE {string.Join(" AND ", conditions)}";

        CompanyUserDetailsRow? user = await connection.QuerySingleOrDefaultAsync<CompanyUserDetailsRow>(
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
                ) AS roles,
                u.created_at,
                u.updated_at
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
                u.is_active,
                u.created_at,
                u.updated_at
            """,
            parameters);

        if (user is null)
            return Errors.General.NotFoundEntity("user").ToFailure();

        return new CompanyUserDetailsResponse(
            user.UserId,
            user.Email,
            user.Username,
            user.DisplayName,
            user.CompanyId,
            user.IsActive,
            user.Roles.Cast<string>().ToArray(),
            user.CreatedAt,
            user.UpdatedAt);
    }

    private sealed record CompanyUserDetailsRow(
        Guid UserId,
        string Email,
        string Username,
        string? DisplayName,
        Guid? CompanyId,
        bool IsActive,
        Array Roles,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
