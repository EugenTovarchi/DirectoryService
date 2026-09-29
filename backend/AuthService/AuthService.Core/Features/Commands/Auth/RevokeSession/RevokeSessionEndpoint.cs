using AuthService.Contracts.Requests;
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

namespace AuthService.Core.Features.Commands.Auth.RevokeSession;

public sealed class RevokeSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/auth/revoke-session",
            async Task<EndpointResult> (
                [FromBody] RevokeSessionRequest request,
                HttpContext httpContext,
                [FromServices] RevokeSessionHandler handler,
                CancellationToken cancellationToken) =>
            {
                Guid userId = httpContext.User.GetUserId();
                string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                RevokeSessionCommand command = new(request, userId, ipAddress);

                return await handler.Handle(command, cancellationToken);
            })
            .RequireAuthorization();
    }
}

public sealed record RevokeSessionCommand(
    RevokeSessionRequest Request,
    Guid UserId,
    string? RevokedByIp) : ICommand;

public sealed class RevokeSessionValidator : AbstractValidator<RevokeSessionCommand>
{
    public RevokeSessionValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.Request.SessionId)
            .NotEmpty();
    }
}

public sealed class RevokeSessionHandler : ICommandHandler<RevokeSessionCommand>
{
    private readonly IOidcSessionService _sessionService;
    private readonly IAuthAuditRepository _auditRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<RevokeSessionCommand> _validator;
    private readonly ILogger<RevokeSessionHandler> _logger;

    public RevokeSessionHandler(
        IOidcSessionService sessionService,
        IAuthAuditRepository auditRepository,
        ITransactionManager transactionManager,
        IValidator<RevokeSessionCommand> validator,
        ILogger<RevokeSessionHandler> logger)
    {
        _sessionService = sessionService;
        _auditRepository = auditRepository;
        _transactionManager = transactionManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<UnitResult<Failure>> Handle(
        RevokeSessionCommand command,
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

        bool revoked = await _sessionService.RevokeSessionAsync(
            command.UserId,
            command.Request.SessionId,
            cancellationToken);
        if (!revoked)
        {
            return UnitResult.Success<Failure>();
        }

        string metadataJson =
            $$"""
              {
                "sessionId": "{{command.Request.SessionId}}"
              }
              """;

        Result<AuthAuditEvent, Error> auditEventResult = AuthAuditEvent.Create(
            companyId: null,
            command.UserId,
            email: null,
            AuthAuditActions.SESSION_REVOKED,
            command.UserId,
            ipAddress: command.RevokedByIp,
            metadataJson);
        if (auditEventResult.IsFailure)
        {
            return auditEventResult.Error.ToFailure();
        }

        UnitResult<Error> addAuditResult = _auditRepository.Add(auditEventResult.Value);
        if (addAuditResult.IsFailure)
            return addAuditResult.Error.ToFailure();

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        UnitResult<Error> commitResult = transactionScope.Commit();
        if (commitResult.IsFailure)
            return commitResult.Error.ToFailure();

        _logger.LogInformation(
            "Session {SessionId} revoke requested for user {UserId}",
            command.Request.SessionId,
            command.UserId);

        return UnitResult.Success<Failure>();
    }
}
