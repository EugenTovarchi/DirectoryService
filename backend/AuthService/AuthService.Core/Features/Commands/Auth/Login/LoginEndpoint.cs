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

namespace AuthService.Core.Features.Commands.Auth.Login;

public sealed class LoginEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/auth/login",
            async Task<EndpointResult<TokenResponse>> (
                [FromBody] LoginRequest request,
                HttpContext httpContext,
                [FromServices] LoginHandler handler,
                CancellationToken cancellationToken) =>
            {
                string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                string? userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();
                var command = new LoginCommand(request, ipAddress, userAgent);
                return await handler.Handle(command, cancellationToken);
            });
    }
}

public sealed record LoginCommand(LoginRequest Request, string? IpAddress, string? UserAgent) : ICommand;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(command => command.Request.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(command => command.Request.Password)
            .NotEmpty();
    }
}

public sealed class LoginHandler : ICommandHandler<TokenResponse, LoginCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRolePermissionReader _rolePermissionReader;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly ITokenService _tokenService;
    private readonly IValidator<LoginCommand> _validator;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        UserManager<ApplicationUser> userManager,
        IRolePermissionReader rolePermissionReader,
        IRefreshTokenRepository refreshTokenRepository,
        ITransactionManager transactionManager,
        ITokenService tokenService,
        IValidator<LoginCommand> validator,
        IOptions<JwtOptions> jwtOptions,
        ILogger<LoginHandler> logger)
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
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        string normalizedEmail = command.Request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user is null)
            return InvalidCredentials();

        if (!user.IsActive)
            return InvalidCredentials();

        bool passwordIsValid = await _userManager.CheckPasswordAsync(user, command.Request.Password);
        if (!passwordIsValid)
            return InvalidCredentials();

        var roles = (await _userManager.GetRolesAsync(user)).ToArray();
        var permissions = await _rolePermissionReader.GetPermissionCodesAsync(roles, cancellationToken);

        var accessToken = _tokenService.CreateAccessToken(user, roles, permissions);
        var refreshToken = _tokenService.CreateRefreshToken();
        DateTime refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenLifetimeDays);

        var session = new RefreshToken(
            user.Id,
            refreshToken.TokenHash,
            refreshTokenExpiresAt,
            command.IpAddress,
            command.UserAgent);

        var addResult = _refreshTokenRepository.Add(session);
        if (addResult.IsFailure)
            return addResult.Error.ToFailure();

        var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} logged in", user.Id);
        }

        return new TokenResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken.RawToken,
            refreshTokenExpiresAt);
    }

    private static Result<TokenResponse, Failure> InvalidCredentials()
    {
        return Errors.User.InvalidCredentials().ToFailure();
    }
}
