using System.Collections.Concurrent;
using AuthService.Core.Abstractions;
using AuthService.Core.Models;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.IntegrationTests.Infrastructure;

public sealed class TestPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly ConcurrentQueue<PasswordResetEmailMessage> _messages = new();

    public IReadOnlyCollection<PasswordResetEmailMessage> Messages => _messages.ToArray();

    public Task<UnitResult<Error>> SendPasswordResetAsync(
        PasswordResetEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(message);

        return Task.FromResult(UnitResult.Success<Error>());
    }
}
