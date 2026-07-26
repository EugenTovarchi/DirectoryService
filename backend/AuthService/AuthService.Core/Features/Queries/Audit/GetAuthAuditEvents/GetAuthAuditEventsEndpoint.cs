using System.Data;
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

namespace AuthService.Core.Features.Queries.Audit.GetAuthAuditEvents;

public sealed class GetAuthAuditEventsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/auth/audit-events",
            async Task<EndpointResult<PagedList<AuthAuditEventResponse>>> (
                [AsParameters] GetAuthAuditEventsRequest request,
                HttpContext httpContext,
                [FromServices] GetAuthAuditEventsHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid requestedByUserId = httpContext.User.GetUserId();
                GetAuthAuditEventsQuery query = new(
                    requestedByUserId,
                    request.CompanyId,
                    request.UserId,
                    request.Action,
                    request.CreatedFromUtc,
                    request.CreatedToUtc,
                    request.Page,
                    request.PageSize);

                return await handler.Handle(query, cancellationToken);
            })
            .RequireAuthorization(AuthPolicies.USERS_MANAGE);
    }
}

public sealed record GetAuthAuditEventsQuery(
    Guid RequestedByUserId,
    Guid? CompanyId,
    Guid? UserId,
    string? Action,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    int Page,
    int PageSize) : IQuery;

public sealed class GetAuthAuditEventsValidator : AbstractValidator<GetAuthAuditEventsQuery>
{
    public GetAuthAuditEventsValidator()
    {
        RuleFor(query => query.RequestedByUserId)
            .NotEmpty();

        RuleFor(query => query.UserId)
            .NotEmpty()
            .When(query => query.UserId.HasValue);

        RuleFor(query => query.CompanyId)
            .NotEmpty()
            .When(query => query.CompanyId.HasValue);

        RuleFor(query => query.Action)
            .MaximumLength(AuthAuditEvent.ACTION_MAX_LENGTH);

        RuleFor(query => query.CreatedToUtc)
            .GreaterThanOrEqualTo(query => query.CreatedFromUtc)
            .When(query => query.CreatedFromUtc.HasValue && query.CreatedToUtc.HasValue);

        RuleFor(query => query.Page)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}

public sealed class GetAuthAuditEventsHandler
    : IQueryHandler<Result<PagedList<AuthAuditEventResponse>, Failure>, GetAuthAuditEventsQuery>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INpgsqlConnectionFactory _connectionFactory;
    private readonly IValidator<GetAuthAuditEventsQuery> _validator;

    public GetAuthAuditEventsHandler(
        UserManager<ApplicationUser> userManager,
        INpgsqlConnectionFactory connectionFactory,
        IValidator<GetAuthAuditEventsQuery> validator)
    {
        _userManager = userManager;
        _connectionFactory = connectionFactory;
        _validator = validator;
    }

    public async Task<Result<PagedList<AuthAuditEventResponse>, Failure>> Handle(
        GetAuthAuditEventsQuery query,
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
        List<string> conditions = [];
        parameters.Add("page_size", query.PageSize);
        parameters.Add("offset", (query.Page - 1) * query.PageSize);

        AddCompanyFilter(query, requestedByUser, requestedBySystemAdmin, parameters, conditions);

        if (query.UserId.HasValue)
        {
            conditions.Add("user_id = @user_id");
            parameters.Add("user_id", query.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            conditions.Add("action = @action");
            parameters.Add("action", query.Action.Trim());
        }

        if (query.CreatedFromUtc.HasValue)
        {
            conditions.Add("created_at >= @created_from_utc");
            parameters.Add("created_from_utc", query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtc.HasValue)
        {
            conditions.Add("created_at <= @created_to_utc");
            parameters.Add("created_to_utc", query.CreatedToUtc.Value);
        }

        string whereClause = conditions.Count > 0
            ? $"WHERE {string.Join(" AND ", conditions)}"
            : string.Empty;

        long totalCount = await connection.ExecuteScalarAsync<long>(
            $"""
            SELECT COUNT(*)
            FROM auth_audit_events
            {whereClause}
            """,
            parameters);

        IEnumerable<AuthAuditEventRow> auditEvents = await connection.QueryAsync<AuthAuditEventRow>(
            $"""
            SELECT
                id AS "Id",
                company_id AS "CompanyId",
                user_id AS "UserId",
                email AS "Email",
                action AS "Action",
                actor_user_id AS "ActorUserId",
                created_at AS "CreatedAt"
            FROM auth_audit_events
            {whereClause}
            ORDER BY created_at DESC, id DESC
            LIMIT @page_size OFFSET @offset
            """,
            parameters);

        return new PagedList<AuthAuditEventResponse>
        {
            Items = auditEvents
                .Select(auditEvent => new AuthAuditEventResponse(
                    auditEvent.Id,
                    auditEvent.CompanyId,
                    auditEvent.UserId,
                    auditEvent.Email,
                    auditEvent.Action,
                    auditEvent.ActorUserId,
                    auditEvent.CreatedAt))
                .ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
        };
    }

    private static void AddCompanyFilter(
        GetAuthAuditEventsQuery query,
        ApplicationUser requestedByUser,
        bool requestedBySystemAdmin,
        DynamicParameters parameters,
        List<string> conditions)
    {
        if (requestedBySystemAdmin)
        {
            if (!query.CompanyId.HasValue)
                return;

            conditions.Add("company_id = @company_id");
            parameters.Add("company_id", query.CompanyId.Value);
            return;
        }

        Guid companyId = requestedByUser.CurrentCompanyId!.Value;
        conditions.Add("company_id = @company_id");
        parameters.Add("company_id", companyId);
    }

    private sealed record AuthAuditEventRow(
        Guid Id,
        Guid? CompanyId,
        Guid? UserId,
        string? Email,
        string Action,
        Guid? ActorUserId,
        DateTime CreatedAt);
}
