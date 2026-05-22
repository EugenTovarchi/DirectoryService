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

namespace AuthService.Core.Features.Commands.Users.ResendInvite;

public sealed class ResendInviteEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/users/{userId:guid}/resend-invite",
            async Task<EndpointResult<ResendInviteResponse>> (
                Guid userId,
                HttpContext httpContext,
                [FromServices] ResendInviteHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid requestedByUserId = httpContext.User.GetUserId();
                ResendInviteCommand command = new(requestedByUserId, userId);

                return await handler.Handle(command, cancellationToken);
            })
            .RequireAuthorization(AuthPolicies.USERS_MANAGE);
    }
}

public sealed record ResendInviteCommand(
    Guid RequestedByUserId,
    Guid UserId) : ICommand;

public sealed class ResendInviteValidator : AbstractValidator<ResendInviteCommand>
{
    public ResendInviteValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}

public sealed class ResendInviteHandler : ICommandHandler<ResendInviteResponse, ResendInviteCommand>
{
    private const int INVITE_TOKEN_LIFETIME_DAYS = 3;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserInviteTokenRepository _inviteTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly InviteLinkFactory _inviteLinkFactory;
    private readonly IInviteEmailSender _inviteEmailSender;
    private readonly IAuthAuditRepository _auditRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<ResendInviteCommand> _validator;
    private readonly ILogger<ResendInviteHandler> _logger;

    public ResendInviteHandler(
        UserManager<ApplicationUser> userManager,
        IUserInviteTokenRepository inviteTokenRepository,
        ITokenService tokenService,
        InviteLinkFactory inviteLinkFactory,
        IInviteEmailSender inviteEmailSender,
        IAuthAuditRepository auditRepository,
        ITransactionManager transactionManager,
        IValidator<ResendInviteCommand> validator,
        ILogger<ResendInviteHandler> logger)
    {
        _userManager = userManager;
        _inviteTokenRepository = inviteTokenRepository;
        _tokenService = tokenService;
        _inviteLinkFactory = inviteLinkFactory;
        _inviteEmailSender = inviteEmailSender;
        _auditRepository = auditRepository;
        _transactionManager = transactionManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<ResendInviteResponse, Failure>> Handle(
        ResendInviteCommand command,
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

        if (targetUser.IsActive || targetUser.PasswordHash is not null)
            return UserManagementFailures.InviteResendIsInvalid();

        Result<ITransactionScope, Error> transactionScopeResult =
            await _transactionManager.BeginTransactionAsync(cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using ITransactionScope transactionScope = transactionScopeResult.Value;

        await _inviteTokenRepository.RevokeActiveTokensForUserAsync(targetUser.Id, cancellationToken);

        RefreshTokenResult inviteToken = _tokenService.CreateRefreshToken();
        DateTime inviteTokenExpiresAt = DateTime.UtcNow.AddDays(INVITE_TOKEN_LIFETIME_DAYS);
        Result<UserInviteToken, Error> inviteTokenResult = UserInviteToken.Create(
            targetUser.Id,
            command.RequestedByUserId,
            inviteToken.TokenHash,
            inviteTokenExpiresAt);
        if (inviteTokenResult.IsFailure)
            return inviteTokenResult.Error.ToFailure();

        UnitResult<Error> addInviteTokenResult = _inviteTokenRepository.Add(inviteTokenResult.Value);
        if (addInviteTokenResult.IsFailure)
            return UserManagementFailures.InviteTokenCreationFailed();

        UnitResult<Error> addAuditResult = _auditRepository.Add(AuthAuditEvent.Create(
            targetUser.CurrentCompanyId,
            targetUser.Id,
            targetUser.Email,
            AuthAuditActions.INVITE_RESENT,
            command.RequestedByUserId).Value);
        if (addAuditResult.IsFailure)
            return addAuditResult.Error.ToFailure();

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        UnitResult<Error> commitResult = transactionScope.Commit();
        if (commitResult.IsFailure)
            return commitResult.Error.ToFailure();

        Uri inviteLink = _inviteLinkFactory.Create(inviteToken.RawToken);
        UnitResult<Error> emailResult = await _inviteEmailSender.SendInviteAsync(
            new InviteEmailMessage(
                targetUser.Id,
                targetUser.Email!,
                targetUser.DisplayName?.Value,
                inviteLink,
                inviteTokenExpiresAt),
            cancellationToken);
        if (emailResult.IsFailure)
            return emailResult.Error.ToFailure();

        string[] roles = (await _userManager.GetRolesAsync(targetUser)).ToArray();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Invite resent for user {UserId} by {RequestedByUserId}",
                targetUser.Id,
                command.RequestedByUserId);
        }

        return new ResendInviteResponse(
            targetUser.Id,
            targetUser.Email!,
            targetUser.UserName!,
            targetUser.DisplayName?.Value,
            targetUser.CurrentCompanyId,
            roles,
            inviteTokenExpiresAt);
    }
}
