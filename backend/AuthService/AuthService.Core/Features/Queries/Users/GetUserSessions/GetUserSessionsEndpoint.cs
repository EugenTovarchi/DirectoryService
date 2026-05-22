using AuthService.Contracts.Responses;
using AuthService.Core.Abstractions;
using AuthService.Core.Authorization;
using AuthService.Core.Extensions;
using AuthService.Core.Failures;
using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
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

namespace AuthService.Core.Features.Queries.Users.GetUserSessions;

public sealed class GetUserSessionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/users/{userId:guid}/sessions",
            async Task<EndpointResult<IReadOnlyList<AuthSessionResponse>>> (
                Guid userId,
                HttpContext httpContext,
                [FromServices] GetUserSessionsHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid requestedByUserId = httpContext.User.GetUserId();
                GetUserSessionsQuery query = new(requestedByUserId, userId);

                return await handler.Handle(query, cancellationToken);
            })
            .RequireAuthorization(AuthPolicies.USERS_MANAGE);
    }
}

public sealed record GetUserSessionsQuery(
    Guid RequestedByUserId,
    Guid UserId) : IQuery;

public sealed class GetUserSessionsValidator : AbstractValidator<GetUserSessionsQuery>
{
    public GetUserSessionsValidator()
    {
        RuleFor(query => query.RequestedByUserId)
            .NotEmpty();

        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}

public sealed class GetUserSessionsHandler
    : IQueryHandler<Result<IReadOnlyList<AuthSessionResponse>, Failure>, GetUserSessionsQuery>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IValidator<GetUserSessionsQuery> _validator;

    public GetUserSessionsHandler(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenRepository refreshTokenRepository,
        IValidator<GetUserSessionsQuery> validator)
    {
        _userManager = userManager;
        _refreshTokenRepository = refreshTokenRepository;
        _validator = validator;
    }

    public async Task<Result<IReadOnlyList<AuthSessionResponse>, Failure>> Handle(
        GetUserSessionsQuery query,
        CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        ApplicationUser? requestedByUser = await _userManager.FindByIdAsync(query.RequestedByUserId.ToString());
        if (requestedByUser is null || !requestedByUser.IsActive)
            return AuthFailures.InvalidAuthenticatedUser();

        if (query.RequestedByUserId == query.UserId)
            return UserManagementFailures.SelfSessionReadIsInvalid();

        bool requestedBySystemAdmin = await _userManager.IsInRoleAsync(requestedByUser, AuthRoles.SYSTEM_ADMIN);
        if (!requestedBySystemAdmin && requestedByUser.CurrentCompanyId is null)
            return UserManagementFailures.InvalidCompanyContextForList();

        ApplicationUser? targetUser = await _userManager.FindByIdAsync(query.UserId.ToString());
        if (targetUser is null)
            return Errors.General.NotFoundEntity("user").ToFailure();

        if (!requestedBySystemAdmin && targetUser.CurrentCompanyId != requestedByUser.CurrentCompanyId)
            return Errors.General.NotFoundEntity("user").ToFailure();

        IReadOnlyList<RefreshToken> sessions = await _refreshTokenRepository.GetActiveSessionsForUserAsync(
            targetUser.Id,
            ct);

        List<AuthSessionResponse> response = sessions
            .Select(session => new AuthSessionResponse(
                session.Id,
                session.CreatedAt,
                session.ExpiresAt,
                session.LastUsedAt,
                session.CreatedByIp,
                session.UserAgent))
            .ToList();

        return response;
    }
}
