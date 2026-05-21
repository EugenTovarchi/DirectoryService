using AuthService.Contracts.Requests;
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
using Microsoft.Extensions.Logging;
using SharedService.Core.Abstractions;
using SharedService.Core.Validation;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace AuthService.Core.Features.Commands.Users.ChangeUserRole;

public sealed class ChangeUserRoleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch(
            "/api/users/{userId:guid}/change-role",
            async Task<EndpointResult<CompanyUserDetailsResponse>> (
                Guid userId,
                [FromBody] ChangeUserRoleRequest request,
                HttpContext httpContext,
                [FromServices] ChangeUserRoleHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid requestedByUserId = httpContext.User.GetUserId();
                ChangeUserRoleCommand command = new(requestedByUserId, userId, request);

                return await handler.Handle(command, cancellationToken);
            })
            .RequireAuthorization(AuthPolicies.USERS_MANAGE);
    }
}

public sealed record ChangeUserRoleCommand(
    Guid RequestedByUserId,
    Guid UserId,
    ChangeUserRoleRequest Request) : ICommand;

public sealed class ChangeUserRoleValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.Request.Role)
            .NotEmpty();
    }
}

public sealed class ChangeUserRoleHandler : ICommandHandler<CompanyUserDetailsResponse, ChangeUserRoleCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<ChangeUserRoleCommand> _validator;
    private readonly ILogger<ChangeUserRoleHandler> _logger;

    public ChangeUserRoleHandler(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ITransactionManager transactionManager,
        IValidator<ChangeUserRoleCommand> validator,
        ILogger<ChangeUserRoleHandler> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _transactionManager = transactionManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<CompanyUserDetailsResponse, Failure>> Handle(
        ChangeUserRoleCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        ApplicationUser? requestedByUser = await _userManager.FindByIdAsync(command.RequestedByUserId.ToString());
        if (requestedByUser is null || !requestedByUser.IsActive)
            return AuthFailures.InvalidAuthenticatedUser();

        if (command.RequestedByUserId == command.UserId)
            return UserManagementFailures.SelfRoleChangeIsInvalid();

        bool requestedBySystemAdmin = await _userManager.IsInRoleAsync(requestedByUser, AuthRoles.SYSTEM_ADMIN);
        if (!requestedBySystemAdmin && requestedByUser.CurrentCompanyId is null)
            return UserManagementFailures.InvalidCompanyContextForList();

        string role = command.Request.Role.Trim();
        bool roleExists = await _roleManager.RoleExistsAsync(role);
        if (!roleExists)
            return UserManagementFailures.InvalidRole();

        if (!requestedBySystemAdmin && string.Equals(role, AuthRoles.SYSTEM_ADMIN, StringComparison.Ordinal))
            return UserManagementFailures.SystemAdminRoleAssignmentIsInvalid();

        ApplicationUser? targetUser = await _userManager.FindByIdAsync(command.UserId.ToString());
        if (targetUser is null)
            return Errors.General.NotFoundEntity("user").ToFailure();

        if (!requestedBySystemAdmin && targetUser.CurrentCompanyId != requestedByUser.CurrentCompanyId)
            return Errors.General.NotFoundEntity("user").ToFailure();

        Result<ITransactionScope, Error> transactionScopeResult =
            await _transactionManager.BeginTransactionAsync(cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using ITransactionScope transactionScope = transactionScopeResult.Value;

        IList<string> currentRoles = await _userManager.GetRolesAsync(targetUser);
        if (!currentRoles.Contains(role, StringComparer.Ordinal))
        {
            IdentityResult addResult = await _userManager.AddToRoleAsync(targetUser, role);
            if (!addResult.Succeeded)
                return UserManagementFailures.RoleChangeFailed();
        }

        string[] rolesToRemove = currentRoles
            .Where(currentRole => !string.Equals(currentRole, role, StringComparison.Ordinal))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            IdentityResult removeResult = await _userManager.RemoveFromRolesAsync(targetUser, rolesToRemove);
            if (!removeResult.Succeeded)
                return UserManagementFailures.RoleChangeFailed();
        }

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        UnitResult<Error> commitResult = transactionScope.Commit();
        if (commitResult.IsFailure)
            return commitResult.Error.ToFailure();

        string[] roles = (await _userManager.GetRolesAsync(targetUser)).ToArray();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "User {UserId} role changed to {Role} by {RequestedByUserId}",
                targetUser.Id,
                role,
                command.RequestedByUserId);
        }

        return new CompanyUserDetailsResponse(
            targetUser.Id,
            targetUser.Email!,
            targetUser.UserName!,
            targetUser.DisplayName?.Value,
            targetUser.CurrentCompanyId,
            targetUser.IsActive,
            roles,
            targetUser.CreatedAt,
            targetUser.UpdatedAt);
    }
}
