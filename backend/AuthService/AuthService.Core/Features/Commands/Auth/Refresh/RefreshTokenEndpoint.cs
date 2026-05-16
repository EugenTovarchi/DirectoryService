using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using AuthService.Core.Abstractions;
using AuthService.Core.Options;
using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.Core.Abstractions;
using SharedService.Core.Validation;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace AuthService.Core.Features.Commands.Auth.Refresh;

public sealed class RefreshTokenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/auth/refresh",
            async Task<EndpointResult<TokenResponse>> (
                [FromBody] RefreshTokenRequest request,
                HttpContext httpContext,
                [FromServices] RefreshTokenHandler handler,
                CancellationToken cancellationToken) =>
            {
                string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                string? userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();
                var command = new RefreshTokenCommand(request, ipAddress, userAgent);
                return await handler.Handle(command, cancellationToken);
            });
    }
}

public sealed record RefreshTokenCommand(
    RefreshTokenRequest Request,
    string? IpAddress,
    string? UserAgent) : ICommand;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(command => command.Request.RefreshToken)
            .NotEmpty();
    }
}

public sealed class RefreshTokenHandler : ICommandHandler<TokenResponse, RefreshTokenCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRolePermissionReader _rolePermissionReader;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly ITokenService _tokenService;
    private readonly IValidator<RefreshTokenCommand> _validator;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<RefreshTokenHandler> _logger;

    public RefreshTokenHandler(
        UserManager<ApplicationUser> userManager,
        IRolePermissionReader rolePermissionReader,
        IRefreshTokenRepository refreshTokenRepository,
        ITransactionManager transactionManager,
        ITokenService tokenService,
        IValidator<RefreshTokenCommand> validator,
        IOptions<JwtOptions> jwtOptions,
        ILogger<RefreshTokenHandler> logger)
    {
        _userManager = userManager;
        _rolePermissionReader = rolePermissionReader;
        _refreshTokenRepository = refreshTokenRepository;
        _transactionManager = transactionManager;
        _tokenService = tokenService;
        _validator = validator;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<Result<TokenResponse, Failure>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        string tokenHash = _tokenService.HashRefreshToken(command.Request.RefreshToken);
        var currentToken = await _refreshTokenRepository.GetByHashAsync(tokenHash, cancellationToken);
        if (currentToken is null)
            return InvalidRefreshToken();

        if (currentToken.RevokedAt is not null)
        {
            await _refreshTokenRepository.RevokeActiveTokensForUserAsync(
                currentToken.UserId,
                command.IpAddress,
                cancellationToken);

            var reuseSaveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
            if (reuseSaveResult.IsFailure)
                return reuseSaveResult.Error.ToFailure();

            _logger.LogWarning("Refresh token reuse detected for user {UserId}", currentToken.UserId);
            return InvalidRefreshToken();
        }

        if (!currentToken.IsActive)
            return InvalidRefreshToken();

        var user = currentToken.User;
        if (!user.IsActive)
            return InvalidRefreshToken();

        string[] roles = (await _userManager.GetRolesAsync(user)).ToArray();
        IReadOnlyCollection<string> permissions = await _rolePermissionReader.GetPermissionCodesAsync(roles, cancellationToken);

        var accessToken = _tokenService.CreateAccessToken(user, roles, permissions);
        var refreshToken = _tokenService.CreateRefreshToken();
        DateTime refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenLifetimeDays);

        var replacementToken = new RefreshToken(
            user.Id,
            refreshToken.TokenHash,
            refreshTokenExpiresAt,
            command.IpAddress,
            command.UserAgent);

        var addResult = _refreshTokenRepository.Add(replacementToken);
        if (addResult.IsFailure)
            return addResult.Error.ToFailure();

        currentToken.MarkUsed();
        currentToken.Revoke(command.IpAddress, replacementToken.Id);

        var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);
        }

        return new TokenResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.RawToken,
            refreshTokenExpiresAt);
    }

    private static Result<TokenResponse, Failure> InvalidRefreshToken()
    {
        return Errors.User.InvalidCredentials().ToFailure();
    }
}
