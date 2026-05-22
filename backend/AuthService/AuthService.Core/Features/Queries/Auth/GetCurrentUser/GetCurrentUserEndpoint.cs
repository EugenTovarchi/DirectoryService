using AuthService.Contracts.Responses;
using AuthService.Core.Abstractions;
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

namespace AuthService.Core.Features.Queries.Auth.GetCurrentUser;

public sealed class GetCurrentUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/auth/me",
            async Task<EndpointResult<CurrentUserResponse>> (
                HttpContext httpContext,
                [FromServices] GetCurrentUserHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid userId = httpContext.User.GetUserId();
                GetCurrentUserQuery query = new(userId);

                return await handler.Handle(query, cancellationToken);
            })
            .RequireAuthorization();
    }
}

public sealed record GetCurrentUserQuery(Guid UserId) : IQuery;

public sealed class GetCurrentUserValidator : AbstractValidator<GetCurrentUserQuery>
{
    public GetCurrentUserValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}

public sealed class GetCurrentUserHandler : IQueryHandler<Result<CurrentUserResponse, Failure>, GetCurrentUserQuery>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRolePermissionReader _rolePermissionReader;
    private readonly IValidator<GetCurrentUserQuery> _validator;

    public GetCurrentUserHandler(
        UserManager<ApplicationUser> userManager,
        IRolePermissionReader rolePermissionReader,
        IValidator<GetCurrentUserQuery> validator)
    {
        _userManager = userManager;
        _rolePermissionReader = rolePermissionReader;
        _validator = validator;
    }

    public async Task<Result<CurrentUserResponse, Failure>> Handle(
        GetCurrentUserQuery query,
        CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        ApplicationUser? user = await _userManager.FindByIdAsync(query.UserId.ToString());
        if (user is null)
            return Errors.General.NotFoundEntity("user").ToFailure();

        if (!user.IsActive)
            return AuthFailures.InvalidAuthenticatedUser();

        string[] roles = (await _userManager.GetRolesAsync(user)).ToArray();
        IReadOnlyCollection<string> permissions = await _rolePermissionReader.GetPermissionCodesAsync(
            roles,
            ct);

        return new CurrentUserResponse(
            user.Id,
            user.Email!,
            user.UserName!,
            user.DisplayName?.Value,
            user.CurrentCompanyId,
            roles,
            permissions);
    }
}
