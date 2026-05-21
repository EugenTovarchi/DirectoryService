using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
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

namespace AuthService.Core.Features.Commands.Auth.AcceptInvite;

public sealed class AcceptInviteEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/auth/accept-invite",
            async Task<EndpointResult<AcceptInviteResponse>> (
                [FromBody] AcceptInviteRequest request,
                [FromServices] AcceptInviteHandler handler,
                CancellationToken cancellationToken) =>
            {
                AcceptInviteCommand command = new(request);

                return await handler.Handle(command, cancellationToken);
            });
    }
}

public sealed record AcceptInviteCommand(AcceptInviteRequest Request) : ICommand;

public sealed class AcceptInviteValidator : AbstractValidator<AcceptInviteCommand>
{
    public AcceptInviteValidator()
    {
        RuleFor(command => command.Request.InviteToken)
            .NotEmpty();

        RuleFor(command => command.Request.Password)
            .NotEmpty();
    }
}

public sealed class AcceptInviteHandler : ICommandHandler<AcceptInviteResponse, AcceptInviteCommand>
{
    private readonly IUserInviteTokenRepository _inviteTokenRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<AcceptInviteCommand> _validator;
    private readonly ILogger<AcceptInviteHandler> _logger;

    public AcceptInviteHandler(
        IUserInviteTokenRepository inviteTokenRepository,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ITransactionManager transactionManager,
        IValidator<AcceptInviteCommand> validator,
        ILogger<AcceptInviteHandler> logger)
    {
        _inviteTokenRepository = inviteTokenRepository;
        _userManager = userManager;
        _tokenService = tokenService;
        _transactionManager = transactionManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<AcceptInviteResponse, Failure>> Handle(
        AcceptInviteCommand command,
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

        string tokenHash = _tokenService.HashRefreshToken(command.Request.InviteToken);
        UserInviteToken? inviteToken = await _inviteTokenRepository.GetByHashAsync(
            tokenHash,
            cancellationToken);
        if (inviteToken is null || !inviteToken.IsActive)
            return UserManagementFailures.InvalidInviteToken();

        ApplicationUser user = inviteToken.User;
        if (user.IsActive)
            return UserManagementFailures.InvalidInviteToken();

        bool hasPassword = await _userManager.HasPasswordAsync(user);
        if (hasPassword)
            return UserManagementFailures.InvalidInviteToken();

        IdentityResult passwordResult = await _userManager.AddPasswordAsync(user, command.Request.Password);
        if (!passwordResult.Succeeded)
            return UserManagementFailures.PasswordAssignmentFailed();

        user.Activate();
        IdentityResult updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return UserManagementFailures.UserCreationFailed();

        inviteToken.Accept();

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        UnitResult<Error> commitResult = transactionScope.Commit();
        if (commitResult.IsFailure)
            return commitResult.Error.ToFailure();

        string[] roles = (await _userManager.GetRolesAsync(user)).ToArray();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Invite accepted for user {UserId}", user.Id);
        }

        return new AcceptInviteResponse(
            user.Id,
            user.Email!,
            user.UserName!,
            user.DisplayName?.Value,
            user.CurrentCompanyId,
            roles);
    }
}
