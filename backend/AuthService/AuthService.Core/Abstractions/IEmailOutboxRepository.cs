using AuthService.Domain.EmailDelivery;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Core.Abstractions;

/// <summary>
/// Хранилище email outbox. Если запущено несколько копий AuthService, каждая запускает свой Quartz job.
/// Резервирование не позволяет этим job одновременно выбрать и отправить одно и то же письмо.
/// </summary>
public interface IEmailOutboxRepository
{
    UnitResult<Error> Add(EmailOutboxMessage message);

    Task<IReadOnlyCollection<EmailOutboxMessage>> ReserveMessagesReadyForDeliveryAsync(
        DateTime now,
        DateTime leaseExpiredBefore,
        int messagesPerRun,
        CancellationToken cancellationToken = default);

    Task<UnitResult<Error>> SaveChangesAsync(CancellationToken cancellationToken = default);
}
