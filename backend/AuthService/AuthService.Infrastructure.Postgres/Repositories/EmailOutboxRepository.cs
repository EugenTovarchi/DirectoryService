using AuthService.Core.Abstractions;
using AuthService.Domain.EmailDelivery;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SharedService.SharedKernel;

namespace AuthService.Infrastructure.Postgres.Repositories;

/// <summary>
/// PostgreSQL repository для transactional email outbox.
/// При нескольких копиях AuthService выбранные строки временно блокируются, а другие Quartz job
/// пропускают их через `FOR UPDATE SKIP LOCKED`, поэтому одно письмо не выбирается одновременно дважды.
/// </summary>
public sealed class EmailOutboxRepository : IEmailOutboxRepository
{
    private readonly AuthServiceDbContext _dbContext;
    private readonly ILogger<EmailOutboxRepository> _logger;

    public EmailOutboxRepository(
        AuthServiceDbContext dbContext,
        ILogger<EmailOutboxRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public UnitResult<Error> Add(EmailOutboxMessage message)
    {
        if (message is null)
            return Errors.General.ValueIsInvalid("message");

        _dbContext.EmailOutboxMessages.Add(message);
        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Атомарно резервирует готовые письма. Если копия сервиса упала после резервирования,
    /// истёкшее время резерва позволяет другой копии снова подобрать эти записи.
    /// </summary>
    public async Task<IReadOnlyCollection<EmailOutboxMessage>> ReserveMessagesReadyForDeliveryAsync(
        DateTime now,
        DateTime leaseExpiredBefore,
        int messagesPerRun,
        CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        List<EmailOutboxMessage> messages = await _dbContext.EmailOutboxMessages
            .FromSqlInterpolated($$"""
                SELECT *
                FROM email_outbox_messages
                WHERE delivered_at IS NULL
                  AND discarded_at IS NULL
                  AND next_attempt_at <= {{now}}
                  AND (processing_started_at IS NULL OR processing_started_at <= {{leaseExpiredBefore}})
                ORDER BY created_at
                FOR UPDATE SKIP LOCKED
                LIMIT {{messagesPerRun}}
                """)
            .ToListAsync(cancellationToken);

        List<EmailOutboxMessage> reservedMessages = [];
        foreach (EmailOutboxMessage message in messages)
        {
            UnitResult<Error> reserveResult = message.MarkProcessing(now);
            if (reserveResult.IsFailure)
            {
                _logger.LogWarning(
                    "Email outbox message {OutboxMessageId} could not be reserved: {ErrorCode}",
                    message.Id,
                    reserveResult.Error.Code);
                continue;
            }

            reservedMessages.Add(message);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return reservedMessages;
    }

    public async Task<UnitResult<Error>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return UnitResult.Success<Error>();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to save email outbox delivery state");
            return Error.Failure("email.outbox.save.failed", "Email outbox state could not be saved");
        }
    }
}
