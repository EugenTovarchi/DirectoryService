using AuthService.Contracts.Requests;
using AuthService.Core.Abstractions;
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

namespace AuthService.Core.Features.Commands.Auth.Logout;

public sealed class LogoutEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/auth/logout",
            async Task<EndpointResult> (
                [FromBody] RefreshTokenRequest request,
                HttpContext httpContext,
                [FromServices] LogoutHandler handler,
                CancellationToken cancellationToken) =>
            {
                string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                LogoutCommand command = new(request, ipAddress);
                return await handler.Handle(command, cancellationToken);
            });
    }
}

public sealed record LogoutCommand(RefreshTokenRequest Request, string? IpAddress) : ICommand;

public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator()
    {
        RuleFor(command => command.Request.RefreshToken)
            .NotEmpty();
    }
}

public sealed class LogoutHandler : ICommandHandler<LogoutCommand>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly ITokenService _tokenService;
    private readonly IValidator<LogoutCommand> _validator;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ITransactionManager transactionManager,
        ITokenService tokenService,
        IValidator<LogoutCommand> validator,
        ILogger<LogoutHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _transactionManager = transactionManager;
        _tokenService = tokenService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<UnitResult<Failure>> Handle(
        LogoutCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        string tokenHash = _tokenService.HashRefreshToken(command.Request.RefreshToken);
        RefreshToken? refreshToken = await _refreshTokenRepository.GetByHashAsync(tokenHash, cancellationToken);
        if (refreshToken is null || !refreshToken.IsActive)
            return UnitResult.Success<Failure>();

        refreshToken.Revoke(command.IpAddress);

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} logged out", refreshToken.UserId);
        }

        return UnitResult.Success<Failure>();
    }
}
