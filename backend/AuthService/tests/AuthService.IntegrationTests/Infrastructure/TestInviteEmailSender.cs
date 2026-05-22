using System.Collections.Concurrent;
using AuthService.Core.Abstractions;
using AuthService.Core.Models;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.IntegrationTests.Infrastructure;

public sealed class TestInviteEmailSender : IInviteEmailSender
{
    private readonly ConcurrentQueue<InviteEmailMessage> _messages = new();

    public IReadOnlyCollection<InviteEmailMessage> Messages => _messages.ToArray();

    public Task<UnitResult<Error>> SendInviteAsync(
        InviteEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(message);

        return Task.FromResult(UnitResult.Success<Error>());
    }
}
