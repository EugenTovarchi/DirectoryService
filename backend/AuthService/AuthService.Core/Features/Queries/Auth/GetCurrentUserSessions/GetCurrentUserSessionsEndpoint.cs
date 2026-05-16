using AuthService.Contracts.Responses;
using AuthService.Core.Abstractions;
using AuthService.Core.Extensions;
using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SharedService.Core.Abstractions;
using SharedService.Core.Validation;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace AuthService.Core.Features.Queries.Auth.GetCurrentUserSessions;

public sealed class GetCurrentUserSessionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/auth/sessions",
            async Task<EndpointResult<IReadOnlyList<AuthSessionResponse>>> (
                HttpContext httpContext,
                [FromServices] GetCurrentUserSessionsHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid userId = httpContext.User.GetUserId();
                GetCurrentUserSessionsQuery query = new(userId);

                return await handler.Handle(query, cancellationToken);
            })
            .RequireAuthorization();
    }
}

public sealed record GetCurrentUserSessionsQuery(Guid UserId) : IQuery;

public sealed class GetCurrentUserSessionsValidator : AbstractValidator<GetCurrentUserSessionsQuery>
{
    public GetCurrentUserSessionsValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}

public sealed class GetCurrentUserSessionsHandler
    : IQueryHandler<Result<IReadOnlyList<AuthSessionResponse>, Failure>, GetCurrentUserSessionsQuery>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IValidator<GetCurrentUserSessionsQuery> _validator;

    public GetCurrentUserSessionsHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IValidator<GetCurrentUserSessionsQuery> validator)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _validator = validator;
    }

    public async Task<Result<IReadOnlyList<AuthSessionResponse>, Failure>> Handle(
        GetCurrentUserSessionsQuery query,
        CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        IReadOnlyList<RefreshToken> sessions = await _refreshTokenRepository.GetActiveSessionsForUserAsync(
            query.UserId,
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
