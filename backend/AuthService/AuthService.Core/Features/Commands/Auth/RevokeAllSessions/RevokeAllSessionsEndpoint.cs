using AuthService.Core.Abstractions;
using AuthService.Core.Extensions;
using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharedService.Core.Abstractions;
using SharedService.Core.Validation;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace AuthService.Core.Features.Commands.Auth.RevokeAllSessions;

public sealed class RevokeAllSessionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/auth/revoke-all-sessions",
            async Task<EndpointResult> (
                HttpContext httpContext,
                [FromServices] RevokeAllSessionsHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid userId = httpContext.User.GetUserId();
                string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                RevokeAllSessionsCommand command = new(userId, ipAddress);

                return await handler.Handle(command, cancellationToken);
            })
            .RequireAuthorization();
    }
}

public sealed record RevokeAllSessionsCommand(Guid UserId, string? RevokedByIp) : ICommand;

public sealed class RevokeAllSessionsValidator : AbstractValidator<RevokeAllSessionsCommand>
{
    public RevokeAllSessionsValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}

public sealed class RevokeAllSessionsHandler : ICommandHandler<RevokeAllSessionsCommand>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthAuditRepository _auditRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<RevokeAllSessionsCommand> _validator;
    private readonly ILogger<RevokeAllSessionsHandler> _logger;

    public RevokeAllSessionsHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IAuthAuditRepository auditRepository,
        ITransactionManager transactionManager,
        IValidator<RevokeAllSessionsCommand> validator,
        ILogger<RevokeAllSessionsHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _auditRepository = auditRepository;
        _transactionManager = transactionManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<UnitResult<Failure>> Handle(
        RevokeAllSessionsCommand command,
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

        await _refreshTokenRepository.RevokeActiveTokensForUserAsync(
            command.UserId,
            command.RevokedByIp,
            cancellationToken);

        UnitResult<Error> addAuditResult = _auditRepository.Add(AuthAuditEvent.Create(
            companyId: null,
            command.UserId,
            email: null,
            AuthAuditActions.ALL_SESSIONS_REVOKED,
            command.UserId,
            ipAddress: command.RevokedByIp).Value);
        if (addAuditResult.IsFailure)
            return addAuditResult.Error.ToFailure();

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        UnitResult<Error> commitResult = transactionScope.Commit();
        if (commitResult.IsFailure)
            return commitResult.Error.ToFailure();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("All sessions revoked for user {UserId}", command.UserId);
        }

        return UnitResult.Success<Failure>();
    }
}
