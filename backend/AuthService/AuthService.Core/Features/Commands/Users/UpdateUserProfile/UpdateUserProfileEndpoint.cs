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

namespace AuthService.Core.Features.Commands.Users.UpdateUserProfile;

public sealed class UpdateUserProfileEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch(
            "/api/users/{userId:guid}/profile",
            async Task<EndpointResult<CompanyUserDetailsResponse>> (
                Guid userId,
                [FromBody] UpdateUserProfileRequest request,
                HttpContext httpContext,
                [FromServices] UpdateUserProfileHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid requestedByUserId = httpContext.User.GetUserId();
                UpdateUserProfileCommand command = new(requestedByUserId, userId, request);

                return await handler.Handle(command, cancellationToken);
            })
            .RequireAuthorization(AuthPolicies.USERS_MANAGE);
    }
}

public sealed record UpdateUserProfileCommand(
    Guid RequestedByUserId,
    Guid UserId,
    UpdateUserProfileRequest Request) : ICommand;

public sealed class UpdateUserProfileValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.Request.DisplayName)
            .Must(displayName => displayName is null || DisplayName.Create(displayName).IsSuccess);
    }
}

public sealed class UpdateUserProfileHandler : ICommandHandler<CompanyUserDetailsResponse, UpdateUserProfileCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<UpdateUserProfileCommand> _validator;
    private readonly ILogger<UpdateUserProfileHandler> _logger;

    public UpdateUserProfileHandler(
        UserManager<ApplicationUser> userManager,
        ITransactionManager transactionManager,
        IValidator<UpdateUserProfileCommand> validator,
        ILogger<UpdateUserProfileHandler> logger)
    {
        _userManager = userManager;
        _transactionManager = transactionManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<CompanyUserDetailsResponse, Failure>> Handle(
        UpdateUserProfileCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        ApplicationUser? requestedByUser = await _userManager.FindByIdAsync(command.RequestedByUserId.ToString());
        if (requestedByUser is null || !requestedByUser.IsActive)
            return AuthFailures.InvalidAuthenticatedUser();

        bool requestedBySystemAdmin = await _userManager.IsInRoleAsync(requestedByUser, AuthRoles.SYSTEM_ADMIN);
        if (!requestedBySystemAdmin && requestedByUser.CurrentCompanyId is null)
            return UserManagementFailures.InvalidCompanyContextForList();

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

        DisplayName? displayName = command.Request.DisplayName is null
            ? null
            : DisplayName.Create(command.Request.DisplayName).Value;
        targetUser.ChangeDisplayName(displayName);

        IdentityResult updateResult = await _userManager.UpdateAsync(targetUser);
        if (!updateResult.Succeeded)
            return UserManagementFailures.UserProfileChangeFailed();

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
                "User {UserId} profile changed by {RequestedByUserId}",
                targetUser.Id,
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
