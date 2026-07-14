using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using AuthService.Core.Abstractions;
using AuthService.Core.Options;
using CSharpFunctionalExtensions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.Core.Abstractions;
using SharedService.Core.Validation;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace AuthService.Core.Features.Commands.Auth.IssueServiceToken;

public sealed class IssueServiceTokenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/auth/service-token",
            async Task<EndpointResult<ServiceTokenResponse>> (
                [FromBody] ClientCredentialsTokenRequest request,
                [FromServices] IssueServiceTokenHandler handler,
                CancellationToken cancellationToken) =>
            {
                var command = new IssueServiceTokenCommand(request);
                return await handler.Handle(command, cancellationToken);
            });
    }
}

public sealed record IssueServiceTokenCommand(ClientCredentialsTokenRequest Request) : ICommand;

public sealed class IssueServiceTokenValidator : AbstractValidator<IssueServiceTokenCommand>
{
    public IssueServiceTokenValidator()
    {
        RuleFor(command => command.Request.ClientId)
            .NotEmpty();

        RuleFor(command => command.Request.ClientSecret)
            .NotEmpty();
    }
}

public sealed class IssueServiceTokenHandler : ICommandHandler<ServiceTokenResponse, IssueServiceTokenCommand>
{
    private readonly ITokenService _tokenService;
    private readonly IValidator<IssueServiceTokenCommand> _validator;
    private readonly ServiceClientOptions _serviceClientOptions;
    private readonly ILogger<IssueServiceTokenHandler> _logger;

    public IssueServiceTokenHandler(
        ITokenService tokenService,
        IValidator<IssueServiceTokenCommand> validator,
        IOptions<ServiceClientOptions> serviceClientOptions,
        ILogger<IssueServiceTokenHandler> logger)
    {
        _tokenService = tokenService;
        _validator = validator;
        _serviceClientOptions = serviceClientOptions.Value;
        _logger = logger;
    }

    public async Task<Result<ServiceTokenResponse, Failure>> Handle(
        IssueServiceTokenCommand command,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.ToErrors();

        ServiceClientDefinition? serviceClient = _serviceClientOptions.Clients.FirstOrDefault(client =>
            string.Equals(client.ClientId, command.Request.ClientId, StringComparison.Ordinal));

        if (serviceClient is null || !string.Equals(
                serviceClient.ClientSecret,
                command.Request.ClientSecret,
                StringComparison.Ordinal))
        {
            _logger.LogWarning("Invalid service token request for client {ClientId}", command.Request.ClientId);
            return Error.Validation("service.credentials.invalid", "Service credentials are invalid").ToFailure();
        }

        AccessTokenResult accessToken = _tokenService.CreateServiceAccessToken(
            serviceClient.ClientId,
            serviceClient.ServiceName,
            serviceClient.ServicePermissions);

        return new ServiceTokenResponse(accessToken.Token, accessToken.ExpiresAt);
    }
}
