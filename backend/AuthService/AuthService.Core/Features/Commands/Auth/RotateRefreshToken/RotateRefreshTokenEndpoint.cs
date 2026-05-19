using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using AuthService.Core.Abstractions;
using AuthService.Core.Failures;
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

namespace AuthService.Core.Features.Commands.Auth.RotateRefreshToken;

public sealed class RotateRefreshTokenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/auth/refresh",
            async Task<EndpointResult<TokenResponse>> (
                [FromBody] RefreshTokenRequest request,
                HttpContext httpContext,
                [FromServices] RotateRefreshTokenHandler handler,
                CancellationToken cancellationToken) =>
            {
                string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                string? userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();
                RotateRefreshTokenCommand command = new(request, ipAddress, userAgent);
                return await handler.Handle(command, cancellationToken);
            });
    }
}

public sealed record RotateRefreshTokenCommand(
    RefreshTokenRequest Request,
    string? IpAddress,
    string? UserAgent) : ICommand;

public sealed class RotateRefreshTokenValidator : AbstractValidator<RotateRefreshTokenCommand>
{
    public RotateRefreshTokenValidator()
    {
        RuleFor(command => command.Request.RefreshToken)
            .NotEmpty();
    }
}

public sealed class RotateRefreshTokenHandler : ICommandHandler<TokenResponse, RotateRefreshTokenCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRolePermissionReader _rolePermissionReader;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly ITokenService _tokenService;
    private readonly IValidator<RotateRefreshTokenCommand> _validator;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<RotateRefreshTokenHandler> _logger;

    public RotateRefreshTokenHandler(
        UserManager<ApplicationUser> userManager,
        IRolePermissionReader rolePermissionReader,
        IRefreshTokenRepository refreshTokenRepository,
        ITransactionManager transactionManager,
        ITokenService tokenService,
        IValidator<RotateRefreshTokenCommand> validator,
        IOptions<JwtOptions> jwtOptions,
        ILogger<RotateRefreshTokenHandler> logger)
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
        RotateRefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        string tokenHash = _tokenService.HashRefreshToken(command.Request.RefreshToken);
        RefreshToken? currentToken = await _refreshTokenRepository.GetByHashAsync(tokenHash, cancellationToken);
        if (currentToken is null)
            return AuthFailures.InvalidRefreshToken();

        if (currentToken.RevokedAt is not null)
        {
            await _refreshTokenRepository.RevokeActiveTokensForUserAsync(
                currentToken.UserId,
                command.IpAddress,
                cancellationToken);

            UnitResult<Error> reuseSaveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
            if (reuseSaveResult.IsFailure)
                return reuseSaveResult.Error.ToFailure();

            _logger.LogWarning("Refresh token reuse detected for user {UserId}", currentToken.UserId);
            return AuthFailures.InvalidRefreshToken();
        }

        if (!currentToken.IsActive)
            return AuthFailures.InvalidRefreshToken();

        ApplicationUser user = currentToken.User;
        if (!user.IsActive)
            return AuthFailures.InvalidRefreshToken();

        string[] roles = (await _userManager.GetRolesAsync(user)).ToArray();
        IReadOnlyCollection<string> permissions = await _rolePermissionReader.GetPermissionCodesAsync(roles, cancellationToken);

        AccessTokenResult accessToken = _tokenService.CreateAccessToken(user, roles, permissions);
        RefreshTokenResult refreshToken = _tokenService.CreateRefreshToken();
        DateTime refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenLifetimeDays);

        Result<RefreshToken, Error> replacementTokenResult = RefreshToken.Create(
            user.Id,
            refreshToken.TokenHash,
            refreshTokenExpiresAt,
            command.IpAddress,
            command.UserAgent);
        if (replacementTokenResult.IsFailure)
            return replacementTokenResult.Error.ToFailure();

        RefreshToken replacementToken = replacementTokenResult.Value;

        UnitResult<Error> addResult = _refreshTokenRepository.Add(replacementToken);
        if (addResult.IsFailure)
            return addResult.Error.ToFailure();

        currentToken.MarkUsed();
        currentToken.Revoke(command.IpAddress, replacementToken.Id);

        UnitResult<Error> saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
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
}
