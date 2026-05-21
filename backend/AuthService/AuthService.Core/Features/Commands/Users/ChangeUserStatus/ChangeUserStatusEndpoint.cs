using AuthService.Contracts.Requests;
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
using Microsoft.Extensions.Logging;
using SharedService.Core.Abstractions;
using SharedService.Core.Validation;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace AuthService.Core.Features.Commands.Users.ChangeUserStatus;

public sealed class ChangeUserStatusEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch(
            "/api/users/{userId:guid}/change-status",
            async Task<EndpointResult<CompanyUserDetailsResponse>> (
                Guid userId,
                [FromBody] ChangeUserStatusRequest request,
                HttpContext httpContext,
                [FromServices] ChangeUserStatusHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid requestedByUserId = httpContext.User.GetUserId();
                ChangeUserStatusCommand command = new(requestedByUserId, userId, request);

                return await handler.Handle(command, cancellationToken);
            })
            .RequireAuthorization(AuthPolicies.USERS_MANAGE);
    }
}

public sealed record ChangeUserStatusCommand(
    Guid RequestedByUserId,
    Guid UserId,
    ChangeUserStatusRequest Request) : ICommand;

public sealed class ChangeUserStatusValidator : AbstractValidator<ChangeUserStatusCommand>
{
    public ChangeUserStatusValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}

public sealed class ChangeUserStatusHandler : ICommandHandler<CompanyUserDetailsResponse, ChangeUserStatusCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IValidator<ChangeUserStatusCommand> _validator;
    private readonly ILogger<ChangeUserStatusHandler> _logger;

    public ChangeUserStatusHandler(
        UserManager<ApplicationUser> userManager,
        IValidator<ChangeUserStatusCommand> validator,
        ILogger<ChangeUserStatusHandler> logger)
    {
        _userManager = userManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<CompanyUserDetailsResponse, Failure>> Handle(
        ChangeUserStatusCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        ApplicationUser? requestedByUser = await _userManager.FindByIdAsync(command.RequestedByUserId.ToString());
        if (requestedByUser is null || !requestedByUser.IsActive)
            return AuthFailures.InvalidAuthenticatedUser();

        if (command.RequestedByUserId == command.UserId && !command.Request.IsActive)
            return UserManagementFailures.SelfDeactivationIsInvalid();

        bool requestedBySystemAdmin = await _userManager.IsInRoleAsync(requestedByUser, AuthRoles.SYSTEM_ADMIN);
        if (!requestedBySystemAdmin && requestedByUser.CurrentCompanyId is null)
            return UserManagementFailures.InvalidCompanyContextForList();

        ApplicationUser? targetUser = await _userManager.FindByIdAsync(command.UserId.ToString());
        if (targetUser is null)
            return Errors.General.NotFoundEntity("user").ToFailure();

        if (!requestedBySystemAdmin && targetUser.CurrentCompanyId != requestedByUser.CurrentCompanyId)
            return Errors.General.NotFoundEntity("user").ToFailure();

        if (command.Request.IsActive)
            targetUser.Activate();
        else
            targetUser.Deactivate();

        IdentityResult updateResult = await _userManager.UpdateAsync(targetUser);
        if (!updateResult.Succeeded)
            return UserManagementFailures.UserStatusChangeFailed();

        string[] roles = (await _userManager.GetRolesAsync(targetUser)).ToArray();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "User {UserId} status changed to {IsActive} by {RequestedByUserId}",
                targetUser.Id,
                targetUser.IsActive,
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
