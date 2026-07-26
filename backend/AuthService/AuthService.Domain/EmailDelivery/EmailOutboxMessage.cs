using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Domain.EmailDelivery;

/// <summary>
/// Письмо в transactional outbox: создаётся в одной транзакции с бизнес-изменением,
/// а доставляется отдельным фоновым обработчиком с повторными попытками.
/// </summary>
public sealed class EmailOutboxMessage
{
    public const int TYPE_MAX_LENGTH = 40;
    public const int EMAIL_MAX_LENGTH = 320;
    public const int DISPLAY_NAME_MAX_LENGTH = 200;
    public const int DELIVERY_LINK_MAX_LENGTH = 2048;
    public const int FAILURE_CODE_MAX_LENGTH = 120;
    public const string DELIVERY_FAILURE_CODE = "email.delivery.failed";
    public const string INVALID_PAYLOAD_FAILURE_CODE = "email.delivery.payload.invalid";

    private EmailOutboxMessage()
    {
    }

    private EmailOutboxMessage(
        string type,
        Guid userId,
        string email,
        string? displayName,
        string deliveryLink,
        DateTime expiresAt)
    {
        Id = Guid.NewGuid();
        Type = type;
        UserId = userId;
        Email = email;
        DisplayName = displayName;
        DeliveryLink = deliveryLink;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
        NextAttemptAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public string Email { get; private set; } = null!;
    public string? DisplayName { get; private set; }
    public string? DeliveryLink { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? ProcessingStartedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? DiscardedAt { get; private set; }
    public string? LastFailureCode { get; private set; }

    /// <summary>
    /// Создаёт outbox-запись для приглашения пользователя.
    /// </summary>
    public static Result<EmailOutboxMessage, Error> CreateInvite(
        Guid userId,
        string email,
        string? displayName,
        Uri inviteLink,
        DateTime expiresAt)
    {
        return Create(
            EmailOutboxMessageTypes.INVITE,
            userId,
            email,
            displayName,
            inviteLink,
            expiresAt);
    }

    /// <summary>
    /// Создаёт outbox-запись для восстановления пароля.
    /// </summary>
    public static Result<EmailOutboxMessage, Error> CreatePasswordReset(
        Guid userId,
        string email,
        string? displayName,
        Uri resetLink,
        DateTime expiresAt)
    {
        return Create(
            EmailOutboxMessageTypes.PASSWORD_RESET,
            userId,
            email,
            displayName,
            resetLink,
            expiresAt);
    }

    /// <summary>
    /// Помечает запись занятой фоновым обработчиком. Ограниченное время резерва позволяет
    /// другому обработчику повторно подобрать письмо после падения процесса.
    /// </summary>
    public UnitResult<Error> MarkProcessing(DateTime startedAt)
    {
        if (IsTerminal())
            return InvalidTransition("Terminal email outbox message cannot be processed");

        if (startedAt < NextAttemptAt)
            return InvalidTransition("Email outbox message is not ready for delivery");

        ProcessingStartedAt = startedAt;
        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Завершает доставку и сразу удаляет secret-bearing ссылку из payload.
    /// </summary>
    public UnitResult<Error> MarkDelivered(DateTime deliveredAt)
    {
        if (IsTerminal())
            return InvalidTransition("Terminal email outbox message cannot be delivered again");

        if (ProcessingStartedAt is null)
            return InvalidTransition("Email outbox message must be reserved before delivery");

        if (deliveredAt < ProcessingStartedAt.Value)
            return InvalidTransition("Delivery time cannot be earlier than processing start time");

        AttemptCount++;
        DeliveredAt = deliveredAt;
        ProcessingStartedAt = null;
        LastFailureCode = null;
        DeliveryLink = null;

        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Регистрирует неудачную попытку и планирует retry. После последней попытки payload очищается.
    /// </summary>
    public UnitResult<Error> RegisterFailure(DateTime failedAt, DateTime nextAttemptAt, int maxAttempts)
    {
        if (IsTerminal())
            return InvalidTransition("Terminal email outbox message cannot register another failure");

        if (ProcessingStartedAt is null)
            return InvalidTransition("Email outbox message must be reserved before registering a failure");

        if (failedAt < ProcessingStartedAt.Value)
            return InvalidTransition("Failure time cannot be earlier than processing start time");

        if (maxAttempts <= 0)
            return Errors.General.ValueIsInvalid("maxAttempts");

        if (nextAttemptAt <= failedAt)
            return Errors.General.ValueIsInvalid("nextAttemptAt");

        AttemptCount++;
        ProcessingStartedAt = null;
        LastFailureCode = DELIVERY_FAILURE_CODE;

        if (AttemptCount >= maxAttempts || failedAt >= ExpiresAt)
        {
            Discard(failedAt);
            return UnitResult.Success<Error>();
        }

        NextAttemptAt = nextAttemptAt;
        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Отбрасывает письмо, если связанный invite/reset token уже истёк, и очищает ссылку.
    /// </summary>
    public UnitResult<Error> DiscardExpired(DateTime discardedAt)
    {
        if (IsTerminal())
            return InvalidTransition("Terminal email outbox message cannot be discarded again");

        if (discardedAt < ExpiresAt)
            return InvalidTransition("Email outbox message cannot be discarded as expired before expiration time");

        Discard(discardedAt);
        return UnitResult.Success<Error>();
    }

    /// <summary>
    /// Завершает повреждённую запись без повторных попыток и очищает потенциально чувствительный payload.
    /// </summary>
    public UnitResult<Error> DiscardInvalidPayload(DateTime discardedAt)
    {
        if (IsTerminal())
            return InvalidTransition("Terminal email outbox message cannot be discarded again");

        LastFailureCode = INVALID_PAYLOAD_FAILURE_CODE;
        Discard(discardedAt);
        return UnitResult.Success<Error>();
    }

    private static Result<EmailOutboxMessage, Error> Create(
        string type,
        Guid userId,
        string email,
        string? displayName,
        Uri deliveryLink,
        DateTime expiresAt)
    {
        if (userId == Guid.Empty)
            return Errors.General.EmptyId(userId);

        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > EMAIL_MAX_LENGTH)
            return Errors.General.ValueIsInvalid("email");

        if (deliveryLink is null || !deliveryLink.IsAbsoluteUri ||
            deliveryLink.AbsoluteUri.Length > DELIVERY_LINK_MAX_LENGTH)
        {
            return Errors.General.ValueIsInvalid("deliveryLink");
        }

        if (expiresAt <= DateTime.UtcNow)
            return Errors.General.ValueIsInvalid("expiresAt");

        string? normalizedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? null
            : displayName.Trim();
        if (normalizedDisplayName?.Length > DISPLAY_NAME_MAX_LENGTH)
            return Errors.General.ValueIsInvalid("displayName");

        return new EmailOutboxMessage(
            type,
            userId,
            email.Trim(),
            normalizedDisplayName,
            deliveryLink.AbsoluteUri,
            expiresAt);
    }

    private void Discard(DateTime discardedAt)
    {
        DiscardedAt = discardedAt;
        ProcessingStartedAt = null;
        DeliveryLink = null;
    }

    private bool IsTerminal()
    {
        return DeliveredAt is not null || DiscardedAt is not null;
    }

    private static Error InvalidTransition(string message)
    {
        return Error.Validation("email.outbox.transition.invalid", message);
    }
}
