using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using AuthService.Core.Abstractions;
using AuthService.Core.Authorization;
using AuthService.Core.Extensions;
using AuthService.Core.Failures;
using AuthService.Core.Models;
using AuthService.Core.Services;
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

namespace AuthService.Core.Features.Commands.Users.InviteUser;

public sealed class InviteUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/users/invite",
            async Task<EndpointResult<InviteUserResponse>> (
                [FromBody] InviteUserRequest request,
                HttpContext httpContext,
                [FromServices] InviteUserHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid invitedByUserId = httpContext.User.GetUserId();
                InviteUserCommand command = new(request, invitedByUserId);

                return await handler.Handle(command, cancellationToken);
            })
            .RequireAuthorization(AuthPolicies.USERS_MANAGE);
    }
}

public sealed record InviteUserCommand(
    InviteUserRequest Request,
    Guid InvitedByUserId) : ICommand;

public sealed class InviteUserValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserValidator()
    {
        RuleFor(command => command.InvitedByUserId)
            .NotEmpty();

        RuleFor(command => command.Request.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(command => command.Request.Username)
            .MustBeValueObject(Username.Create);

        RuleFor(command => command.Request.DisplayName)
            .Must(displayName => displayName is null || DisplayName.Create(displayName).IsSuccess);

        RuleFor(command => command.Request.CompanyId)
            .NotEmpty();

        RuleFor(command => command.Request.Role)
            .NotEmpty();
    }
}

public sealed class InviteUserHandler : ICommandHandler<InviteUserResponse, InviteUserCommand>
{
    private const int INVITE_TOKEN_LIFETIME_DAYS = 3;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IUserInviteTokenRepository _inviteTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly InviteLinkFactory _inviteLinkFactory;
    private readonly IInviteEmailSender _inviteEmailSender;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<InviteUserCommand> _validator;
    private readonly ILogger<InviteUserHandler> _logger;

    public InviteUserHandler(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IUserInviteTokenRepository inviteTokenRepository,
        ITokenService tokenService,
        InviteLinkFactory inviteLinkFactory,
        IInviteEmailSender inviteEmailSender,
        ITransactionManager transactionManager,
        IValidator<InviteUserCommand> validator,
        ILogger<InviteUserHandler> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _inviteTokenRepository = inviteTokenRepository;
        _tokenService = tokenService;
        _inviteLinkFactory = inviteLinkFactory;
        _inviteEmailSender = inviteEmailSender;
        _transactionManager = transactionManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<InviteUserResponse, Failure>> Handle(
        InviteUserCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        ApplicationUser? inviter = await _userManager.FindByIdAsync(command.InvitedByUserId.ToString());
        if (inviter is null || !inviter.IsActive)
            return AuthFailures.InvalidAuthenticatedUser();

        bool inviterIsSystemAdmin = await _userManager.IsInRoleAsync(inviter, AuthRoles.SYSTEM_ADMIN);
        if (!inviterIsSystemAdmin && inviter.CurrentCompanyId != command.Request.CompanyId)
            return UserManagementFailures.InvalidCompanyContext();

        string role = command.Request.Role.Trim();
        bool roleExists = await _roleManager.RoleExistsAsync(role);
        if (!roleExists)
            return UserManagementFailures.InvalidRole();

        string normalizedEmail = command.Request.Email.Trim();
        Username username = Username.Create(command.Request.Username).Value;
        DisplayName? displayName = command.Request.DisplayName is null
            ? null
            : DisplayName.Create(command.Request.DisplayName).Value;

        Result<ITransactionScope, Error> transactionScopeResult =
            await _transactionManager.BeginTransactionAsync(cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using ITransactionScope transactionScope = transactionScopeResult.Value;

        ApplicationUser invitedUser = new(
            normalizedEmail,
            username,
            displayName,
            command.Request.CompanyId);
        invitedUser.Deactivate();

        IdentityResult createResult = await _userManager.CreateAsync(invitedUser);
        if (!createResult.Succeeded)
            return UserManagementFailures.UserCreationFailed();

        IdentityResult roleResult = await _userManager.AddToRoleAsync(invitedUser, role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(invitedUser);
            return UserManagementFailures.RoleAssignmentFailed();
        }

        await _inviteTokenRepository.RevokeActiveTokensForUserAsync(invitedUser.Id, cancellationToken);

        RefreshTokenResult inviteToken = _tokenService.CreateRefreshToken();
        DateTime inviteTokenExpiresAt = DateTime.UtcNow.AddDays(INVITE_TOKEN_LIFETIME_DAYS);
        Result<UserInviteToken, Error> inviteTokenResult = UserInviteToken.Create(
            invitedUser.Id,
            command.InvitedByUserId,
            inviteToken.TokenHash,
            inviteTokenExpiresAt);
        if (inviteTokenResult.IsFailure)
        {
            await _userManager.DeleteAsync(invitedUser);
            return inviteTokenResult.Error.ToFailure();
        }

        UnitResult<Error> addInviteTokenResult = _inviteTokenRepository.Add(inviteTokenResult.Value);
        if (addInviteTokenResult.IsFailure)
        {
            await _userManager.DeleteAsync(invitedUser);
            return UserManagementFailures.InviteTokenCreationFailed();
        }

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        UnitResult<Error> commitResult = transactionScope.Commit();
        if (commitResult.IsFailure)
            return commitResult.Error.ToFailure();

        Uri inviteLink = _inviteLinkFactory.Create(inviteToken.RawToken);
        UnitResult<Error> emailResult = await _inviteEmailSender.SendInviteAsync(
            new InviteEmailMessage(
                invitedUser.Id,
                invitedUser.Email!,
                invitedUser.DisplayName?.Value,
                inviteLink,
                inviteTokenExpiresAt),
            cancellationToken);
        if (emailResult.IsFailure)
            return emailResult.Error.ToFailure();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "User {UserId} invited by {InvitedByUserId} to company {CompanyId}",
                invitedUser.Id,
                command.InvitedByUserId,
                command.Request.CompanyId);
        }

        return new InviteUserResponse(
            invitedUser.Id,
            invitedUser.Email!,
            invitedUser.UserName!,
            invitedUser.DisplayName?.Value,
            command.Request.CompanyId,
            [role],
            inviteTokenExpiresAt);
    }
}
