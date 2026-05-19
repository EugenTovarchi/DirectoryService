using AuthService.Contracts.Responses;
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
using Microsoft.EntityFrameworkCore;
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
            async Task<EndpointResult<IReadOnlyList<UserSummaryResponse>>> (
                HttpContext httpContext,
                [FromServices] GetUsersHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid requestedByUserId = httpContext.User.GetUserId();
                GetUsersQuery query = new(requestedByUserId);

                return await handler.Handle(query, cancellationToken);
            })
            .RequireAuthorization(AuthPolicies.USERS_MANAGE);
    }
}

public sealed record GetUsersQuery(Guid RequestedByUserId) : IQuery;

public sealed class GetUsersValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersValidator()
    {
        RuleFor(query => query.RequestedByUserId)
            .NotEmpty();
    }
}

public sealed class GetUsersHandler : IQueryHandler<Result<IReadOnlyList<UserSummaryResponse>, Failure>, GetUsersQuery>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<GetUsersQuery> _validator;

    public GetUsersHandler(
        UserManager<ApplicationUser> userManager,
        IValidator<GetUsersQuery> validator)
    {
        _userManager = userManager;
        _validator = validator;
    }

    public async Task<Result<IReadOnlyList<UserSummaryResponse>, Failure>> Handle(
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

        IQueryable<ApplicationUser> usersQuery = _userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.Email);

        if (!requestedBySystemAdmin)
        {
            Guid companyId = requestedByUser.CurrentCompanyId!.Value;
            usersQuery = usersQuery.Where(user => user.CurrentCompanyId == companyId);
        }

        List<ApplicationUser> users = await usersQuery.ToListAsync(ct);
        List<UserSummaryResponse> response = new(users.Count);

        foreach (ApplicationUser user in users)
        {
            string[] roles = (await _userManager.GetRolesAsync(user)).ToArray();

            response.Add(new UserSummaryResponse(
                user.Id,
                user.Email!,
                user.UserName!,
                user.DisplayName?.Value,
                user.CurrentCompanyId,
                user.IsActive,
                roles));
        }

        return response;
    }
}
