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

namespace AuthService.Core.Features.Commands.Users.RevokeUserSessions;

public sealed class RevokeUserSessionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/users/{userId:guid}/revoke-sessions",
            async Task<EndpointResult> (
                Guid userId,
                HttpContext httpContext,
                [FromServices] RevokeUserSessionsHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid requestedByUserId = httpContext.User.GetUserId();
                string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                RevokeUserSessionsCommand command = new(requestedByUserId, userId, ipAddress);

                return await handler.Handle(command, cancellationToken);
            })
            .RequireAuthorization(AuthPolicies.USERS_MANAGE);
    }
}

public sealed record RevokeUserSessionsCommand(
    Guid RequestedByUserId,
    Guid UserId,
    string? RevokedByIp) : ICommand;

public sealed class RevokeUserSessionsValidator : AbstractValidator<RevokeUserSessionsCommand>
{
    public RevokeUserSessionsValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}

public sealed class RevokeUserSessionsHandler : ICommandHandler<RevokeUserSessionsCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<RevokeUserSessionsCommand> _validator;
    private readonly ILogger<RevokeUserSessionsHandler> _logger;

    public RevokeUserSessionsHandler(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenRepository refreshTokenRepository,
        ITransactionManager transactionManager,
        IValidator<RevokeUserSessionsCommand> validator,
        ILogger<RevokeUserSessionsHandler> logger)
    {
        _userManager = userManager;
        _refreshTokenRepository = refreshTokenRepository;
        _transactionManager = transactionManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<UnitResult<Failure>> Handle(
        RevokeUserSessionsCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        ApplicationUser? requestedByUser = await _userManager.FindByIdAsync(command.RequestedByUserId.ToString());
        if (requestedByUser is null || !requestedByUser.IsActive)
            return AuthFailures.InvalidAuthenticatedUser();

        if (command.RequestedByUserId == command.UserId)
            return UserManagementFailures.SelfSessionRevocationIsInvalid();

        bool requestedBySystemAdmin = await _userManager.IsInRoleAsync(requestedByUser, AuthRoles.SYSTEM_ADMIN);
        if (!requestedBySystemAdmin && requestedByUser.CurrentCompanyId is null)
            return UserManagementFailures.InvalidCompanyContextForList();

        ApplicationUser? targetUser = await _userManager.FindByIdAsync(command.UserId.ToString());
        if (targetUser is null)
            return Errors.General.NotFoundEntity("user").ToFailure();

        if (!requestedBySystemAdmin && targetUser.CurrentCompanyId != requestedByUser.CurrentCompanyId)
            return Errors.General.NotFoundEntity("user").ToFailure();

        await _refreshTokenRepository.RevokeActiveTokensForUserAsync(
            targetUser.Id,
            command.RevokedByIp,
            cancellationToken);

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "All sessions revoked for user {UserId} by {RequestedByUserId}",
                targetUser.Id,
                command.RequestedByUserId);
        }

        return UnitResult.Success<Failure>();
    }
}
