using AuthService.Core.Abstractions;
using AuthService.Core.Models;
using AuthService.Core.Options;
using AuthService.Domain.EmailDelivery;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedService.SharedKernel;

namespace AuthService.Infrastructure.Postgres.EmailDelivery;

/// <summary>
/// Выполняет один проход по email outbox: резервирование, SMTP delivery и сохранение результата попытки.
/// Класс отделён от Quartz, поэтому его легко вызывать в integration tests и из другого scheduler-а.
/// </summary>
public sealed class EmailOutboxProcessor
{
    private readonly IEmailOutboxRepository _outboxRepository;
    private readonly IInviteEmailSender _inviteEmailSender;
    private readonly IPasswordResetEmailSender _passwordResetEmailSender;
    private readonly EmailOutboxOptions _options;
    private readonly ILogger<EmailOutboxProcessor> _logger;

    public EmailOutboxProcessor(
        IEmailOutboxRepository outboxRepository,
        IInviteEmailSender inviteEmailSender,
        IPasswordResetEmailSender passwordResetEmailSender,
        IOptions<EmailOutboxOptions> options,
        ILogger<EmailOutboxProcessor> logger)
    {
        _outboxRepository = outboxRepository;
        _inviteEmailSender = inviteEmailSender;
        _passwordResetEmailSender = passwordResetEmailSender;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Обрабатывает не больше настроенного числа писем за один запуск и возвращает число зарезервированных сообщений.
    /// </summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        DateTime leaseExpiredBefore = now.AddSeconds(-_options.ProcessingLeaseSeconds);
        IReadOnlyCollection<EmailOutboxMessage> messages =
            await _outboxRepository.ReserveMessagesReadyForDeliveryAsync(
                now,
                leaseExpiredBefore,
                _options.MessagesPerRun,
                cancellationToken);

        foreach (EmailOutboxMessage message in messages)
            await ProcessMessageAsync(message, cancellationToken);

        return messages.Count;
    }

    private async Task ProcessMessageAsync(
        EmailOutboxMessage message,
        CancellationToken cancellationToken)
    {
        DateTime attemptTime = DateTime.UtcNow;
        if (attemptTime >= message.ExpiresAt)
        {
            UnitResult<Error> discardResult = message.DiscardExpired(attemptTime);
            if (discardResult.IsFailure)
            {
                LogInvalidTransition(message, discardResult.Error);
                return;
            }

            UnitResult<Error> discardSaveResult = await _outboxRepository.SaveChangesAsync(cancellationToken);
            if (discardSaveResult.IsFailure)
            {
                _logger.LogError(
                    "Failed to persist discarded state for email outbox message {OutboxMessageId}",
                    message.Id);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(message.DeliveryLink))
        {
            UnitResult<Error> discardResult = message.DiscardInvalidPayload(attemptTime);
            if (discardResult.IsFailure)
            {
                LogInvalidTransition(message, discardResult.Error);
                return;
            }

            await _outboxRepository.SaveChangesAsync(cancellationToken);
            return;
        }

        UnitResult<Error> deliveryResult = await SendAsync(message, cancellationToken);
        if (deliveryResult.IsSuccess)
        {
            UnitResult<Error> deliveredResult = message.MarkDelivered(DateTime.UtcNow);
            if (deliveredResult.IsFailure)
            {
                LogInvalidTransition(message, deliveredResult.Error);
                return;
            }

            UnitResult<Error> deliverySaveResult = await _outboxRepository.SaveChangesAsync(cancellationToken);
            if (deliverySaveResult.IsFailure)
            {
                _logger.LogError(
                    "Failed to persist delivered state for email outbox message {OutboxMessageId}",
                    message.Id);
                return;
            }

            _logger.LogInformation(
                "Email outbox message {OutboxMessageId} of type {MessageType} delivered for user {UserId}",
                message.Id,
                message.Type,
                message.UserId);
            return;
        }

        TimeSpan retryDelay = CalculateRetryDelay(message.AttemptCount);
        UnitResult<Error> failureResult = message.RegisterFailure(
            DateTime.UtcNow,
            DateTime.UtcNow.Add(retryDelay),
            _options.MaxAttempts);
        if (failureResult.IsFailure)
        {
            LogInvalidTransition(message, failureResult.Error);
            return;
        }

        UnitResult<Error> failureSaveResult = await _outboxRepository.SaveChangesAsync(cancellationToken);
        if (failureSaveResult.IsFailure)
        {
            _logger.LogError(
                "Failed to persist retry state for email outbox message {OutboxMessageId}",
                message.Id);
            return;
        }

        _logger.LogWarning(
            "Email outbox message {OutboxMessageId} of type {MessageType} failed on attempt {AttemptCount}",
            message.Id,
            message.Type,
            message.AttemptCount);
    }

    private Task<UnitResult<Error>> SendAsync(
        EmailOutboxMessage message,
        CancellationToken cancellationToken)
    {
        Uri deliveryLink = new(message.DeliveryLink!, UriKind.Absolute);

        return message.Type switch
        {
            EmailOutboxMessageTypes.INVITE => _inviteEmailSender.SendInviteAsync(
                new InviteEmailMessage(
                    message.UserId,
                    message.Email,
                    message.DisplayName,
                    deliveryLink,
                    message.ExpiresAt),
                cancellationToken),
            EmailOutboxMessageTypes.PASSWORD_RESET => _passwordResetEmailSender.SendPasswordResetAsync(
                new PasswordResetEmailMessage(
                    message.UserId,
                    message.Email,
                    message.DisplayName,
                    deliveryLink,
                    message.ExpiresAt),
                cancellationToken),
            _ => Task.FromResult(UnitResult.Failure(
                Error.Failure("email.outbox.type.unsupported", "Email outbox message type is unsupported")))
        };
    }

    private TimeSpan CalculateRetryDelay(int completedAttempts)
    {
        double multiplier = Math.Pow(2, completedAttempts);
        double delaySeconds = Math.Min(
            _options.InitialRetryDelaySeconds * multiplier,
            _options.MaxRetryDelaySeconds);

        return TimeSpan.FromSeconds(delaySeconds);
    }

    private void LogInvalidTransition(EmailOutboxMessage message, Error error)
    {
        _logger.LogError(
            "Email outbox message {OutboxMessageId} rejected state transition {ErrorCode}",
            message.Id,
            error.Code);
    }
}
