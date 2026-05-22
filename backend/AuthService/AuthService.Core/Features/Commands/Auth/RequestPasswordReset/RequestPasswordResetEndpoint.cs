using AuthService.Contracts.Requests;
using AuthService.Core.Abstractions;
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

namespace AuthService.Core.Features.Commands.Auth.RequestPasswordReset;

public sealed class RequestPasswordResetEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/auth/request-password-reset",
            async Task<EndpointResult> (
                [FromBody] RequestPasswordResetRequest request,
                [FromServices] RequestPasswordResetHandler handler,
                CancellationToken cancellationToken) =>
            {
                RequestPasswordResetCommand command = new(request);

                return await handler.Handle(command, cancellationToken);
            });
    }
}

public sealed record RequestPasswordResetCommand(RequestPasswordResetRequest Request) : ICommand;

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(command => command.Request.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

public sealed class RequestPasswordResetHandler : ICommandHandler<RequestPasswordResetCommand>
{
    private const int PASSWORD_RESET_TOKEN_LIFETIME_HOURS = 1;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly PasswordResetLinkFactory _passwordResetLinkFactory;
    private readonly IPasswordResetEmailSender _passwordResetEmailSender;
    private readonly IAuthAuditRepository _auditRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<RequestPasswordResetCommand> _validator;
    private readonly ILogger<RequestPasswordResetHandler> _logger;

    public RequestPasswordResetHandler(
        UserManager<ApplicationUser> userManager,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        ITokenService tokenService,
        PasswordResetLinkFactory passwordResetLinkFactory,
        IPasswordResetEmailSender passwordResetEmailSender,
        IAuthAuditRepository auditRepository,
        ITransactionManager transactionManager,
        IValidator<RequestPasswordResetCommand> validator,
        ILogger<RequestPasswordResetHandler> logger)
    {
        _userManager = userManager;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _tokenService = tokenService;
        _passwordResetLinkFactory = passwordResetLinkFactory;
        _passwordResetEmailSender = passwordResetEmailSender;
        _auditRepository = auditRepository;
        _transactionManager = transactionManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<UnitResult<Failure>> Handle(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        string normalizedEmail = command.Request.Email.Trim();
        ApplicationUser? user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null || !user.IsActive || !await _userManager.HasPasswordAsync(user))
            return UnitResult.Success<Failure>();

        Result<ITransactionScope, Error> transactionScopeResult =
            await _transactionManager.BeginTransactionAsync(cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using ITransactionScope transactionScope = transactionScopeResult.Value;

        await _passwordResetTokenRepository.RevokeActiveTokensForUserAsync(user.Id, cancellationToken);

        RefreshTokenResult resetToken = _tokenService.CreateRefreshToken();
        DateTime resetTokenExpiresAt = DateTime.UtcNow.AddHours(PASSWORD_RESET_TOKEN_LIFETIME_HOURS);
        Result<PasswordResetToken, Error> resetTokenResult = PasswordResetToken.Create(
            user.Id,
            resetToken.TokenHash,
            resetTokenExpiresAt);
        if (resetTokenResult.IsFailure)
            return resetTokenResult.Error.ToFailure();

        UnitResult<Error> addTokenResult = _passwordResetTokenRepository.Add(resetTokenResult.Value);
        if (addTokenResult.IsFailure)
            return UserManagementFailures.PasswordResetTokenCreationFailed();

        UnitResult<Error> addAuditResult = _auditRepository.Add(AuthAuditEvent.Create(
            user.CurrentCompanyId,
            user.Id,
            user.Email,
            AuthAuditActions.PASSWORD_RESET_REQUESTED,
            actorUserId: null).Value);
        if (addAuditResult.IsFailure)
            return addAuditResult.Error.ToFailure();

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        UnitResult<Error> commitResult = transactionScope.Commit();
        if (commitResult.IsFailure)
            return commitResult.Error.ToFailure();

        Uri resetLink = _passwordResetLinkFactory.Create(resetToken.RawToken);
        UnitResult<Error> emailResult = await _passwordResetEmailSender.SendPasswordResetAsync(
            new PasswordResetEmailMessage(
                user.Id,
                user.Email!,
                user.DisplayName?.Value,
                resetLink,
                resetTokenExpiresAt),
            cancellationToken);

        if (emailResult.IsFailure && _logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(
                "Password reset email delivery failed after token creation for user {UserId}",
                user.Id);
        }

        return UnitResult.Success<Failure>();
    }
}
