using System.Collections.Concurrent;
using AuthService.Core.Abstractions;
using AuthService.Core.Models;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.IntegrationTests.Infrastructure;

public sealed class TestInviteEmailSender : IInviteEmailSender
{
    private readonly ConcurrentQueue<InviteEmailMessage> _messages = new();
    private int _failuresRemaining;

    public IReadOnlyCollection<InviteEmailMessage> Messages => _messages.ToArray();

    /// <summary>
    /// Настраивает заданное число следующих ошибок доставки для проверки retry без настоящего SMTP.
    /// </summary>
    public void FailNextAttempts(int attemptCount)
    {
        Interlocked.Exchange(ref _failuresRemaining, attemptCount);
    }

    public Task<UnitResult<Error>> SendInviteAsync(
        InviteEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _failuresRemaining) > 0)
        {
            Interlocked.Decrement(ref _failuresRemaining);
            return Task.FromResult(UnitResult.Failure(
                Error.Failure("test.email.delivery.failed", "Configured test delivery failure")));
        }

        _messages.Enqueue(message);

        return Task.FromResult(UnitResult.Success<Error>());
    }
}
