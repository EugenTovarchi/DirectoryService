using AuthService.Contracts.Requests;
using AuthService.Core.Abstractions;
using AuthService.Domain.Users;
using CSharpFunctionalExtensions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharedService.Core.Abstractions;
using SharedService.Core.Validation;
using SharedService.Framework.EndpointSettings;
using SharedService.SharedKernel;

namespace AuthService.Core.Features.Commands.Users.RegisterUser;

public sealed class RegisterUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/auth/users",
            async Task<EndpointResult<Guid>> (
                [FromBody] RegisterUserRequest request,
                [FromServices] RegisterUserHandler handler,
                CancellationToken cancellationToken) =>
            {
                var command = new RegisterUserCommand(request);
                return await handler.Handle(command, cancellationToken);
            });
    }
}

public record RegisterUserCommand(RegisterUserRequest Request) : ICommand;

public class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Request.Email)
            .MustBeValueObject(Email.Create);

        RuleFor(x => x.Request.Username)
            .MustBeValueObject(Username.Create);

        RuleFor(x => x.Request.PasswordHash)
            .MustBeValueObject(PasswordHash.Create);
    }
}

public class RegisterUserHandler : ICommandHandler<Guid, RegisterUserCommand>
{
    private readonly IAuthUserRepository _authUserRepository;
    private readonly ITransactionManager _transactionManager;
    private readonly IValidator<RegisterUserCommand> _validator;
    private readonly ILogger<RegisterUserHandler> _logger;

    public RegisterUserHandler(
        IAuthUserRepository authUserRepository,
        ITransactionManager transactionManager,
        IValidator<RegisterUserCommand> validator,
        ILogger<RegisterUserHandler> logger)
    {
        _authUserRepository = authUserRepository;
        _transactionManager = transactionManager;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid, Failure>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        if (command is null)
            return Errors.General.ValueIsInvalid("command").ToFailure();

        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Register user command is invalid for email {Email}", command.Request.Email);
            return validationResult.ToErrors();
        }

        var email = Email.Create(command.Request.Email).Value;
        var username = Username.Create(command.Request.Username).Value;
        var passwordHash = PasswordHash.Create(command.Request.PasswordHash).Value;

        var existingUser = await _authUserRepository.GetBy(
            user => user.Email.Value == email.Value || user.Username.Value == username.Value,
            cancellationToken);

        if (existingUser.IsSuccess)
            return Errors.General.Duplicate("user").ToFailure();

        var userResult = AuthUser.Create(email, username, passwordHash);
        if (userResult.IsFailure)
            return userResult.Error.ToFailure();

        var user = userResult.Value;
        var addResult = _authUserRepository.Add(user);
        if (addResult.IsFailure)
            return addResult.Error.ToFailure();

        var saveResult = await _transactionManager.SaveChangeAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error.ToFailure();

        var userId = user.Id;
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Auth user {UserId} was registered", userId);
        }

        return userId;
    }
}
