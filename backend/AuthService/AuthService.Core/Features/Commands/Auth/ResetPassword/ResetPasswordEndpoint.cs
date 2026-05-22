using AuthService.Contracts.Requests;
using AuthService.Core.Abstractions;
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

namespace AuthService.Core.Features.Commands.Auth.ResetPassword;

public sealed class ResetPasswordEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/auth/reset-password",
            async Task<EndpointResult> (
                [FromBody] ResetPasswordRequest request,
                [FromServices] ResetPasswordHandler handler,
                CancellationToken cancellationToken) =>
            {
                ResetPasswordCommand command = new(request);

                return await handler.Handle(command, cancellationToken);
            });
    }
}

public sealed record ResetPasswordCommand(ResetPasswordRequest Request) : ICommand;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(command => command.Request.Token)
            .NotEmpty();

        RuleFor(command => command.Request.Password)
            .NotEmpty();
    }
}

public sealed class ResetPasswordHandler : ICommandHandler<ResetPasswordCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<ResetPasswordCommand> _validator;
    private readonly ILogger<ResetPasswordHandler> _logger;

    public ResetPasswordHandler(
        UserManager<ApplicationUser> userManager,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        ITransactionManager transactionManager,
        IValidator<ResetPasswordCommand> validator,
        ILogger<ResetPasswordHandler> logger)
    {
        _userManager = userManager;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _transactionManager = transactionManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<UnitResult<Failure>> Handle(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        Result<ITransactionScope, Error> transactionScopeResult =
            await _transactionManager.BeginTransactionAsync(cancellationToken);
        if (transactionScopeResult.IsFailure)
            return transactionScopeResult.Error.ToFailure();

        using ITransactionScope transactionScope = transactionScopeResult.Value;

        string tokenHash = _tokenService.HashRefreshToken(command.Request.Token);
        PasswordResetToken? resetToken = await _passwordResetTokenRepository.GetByHashAsync(
            tokenHash,
            cancellationToken);
        if (resetToken is null || !resetToken.IsActive)
            return UserManagementFailures.InvalidPasswordResetToken();

        ApplicationUser user = resetToken.User;
        if (!user.IsActive || !await _userManager.HasPasswordAsync(user))
            return UserManagementFailures.InvalidPasswordResetToken();

        IdentityResult removePasswordResult = await _userManager.RemovePasswordAsync(user);
        if (!removePasswordResult.Succeeded)
            return UserManagementFailures.PasswordResetFailed();

        IdentityResult addPasswordResult = await _userManager.AddPasswordAsync(user, command.Request.Password);
        if (!addPasswordResult.Succeeded)
            return UserManagementFailures.PasswordResetFailed();

        resetToken.MarkUsed();
        await _passwordResetTokenRepository.RevokeActiveTokensForUserAsync(user.Id, cancellationToken);
        await _refreshTokenRepository.RevokeActiveTokensForUserAsync(
            user.Id,
            revokedByIp: null,
            cancellationToken);

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        UnitResult<Error> commitResult = transactionScope.Commit();
        if (commitResult.IsFailure)
            return commitResult.Error.ToFailure();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Password reset completed for user {UserId}", user.Id);
        }

        return UnitResult.Success<Failure>();
    }
}
