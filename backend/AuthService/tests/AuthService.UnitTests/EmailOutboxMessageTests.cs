using AuthService.Domain.EmailDelivery;
using FluentAssertions;

namespace AuthService.UnitTests;

/// <summary>
/// Эти тесты показывают универсальный способ проверки outbox entity без БД и Quartz:
/// отдельно создаём состояние, выполняем один переход и проверяем его результат.
/// </summary>
public sealed class EmailOutboxMessageTests
{
    [Fact]
    public void CreateInvite_WithValidData_ShouldCreateMessageReadyForFirstDeliveryAttempt()
    {
        // Arrange: готовим безопасные данные письма и срок жизни связанного invite token.
        Guid userId = Guid.NewGuid();
        DateTime expiresAt = DateTime.UtcNow.AddDays(3);
        var inviteLink = new Uri("https://app.example.com/invite?token=secret-token");

        // Act: создаём outbox message тем же factory method, который использует application handler.
        var result = EmailOutboxMessage.CreateInvite(
            userId,
            "user@example.com",
            "Test User",
            inviteLink,
            expiresAt);

        // Assert: новая запись ещё не обработана и готова к первой попытке доставки.
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(EmailOutboxMessageTypes.INVITE);
        result.Value.AttemptCount.Should().Be(0);
        result.Value.NextAttemptAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        result.Value.DeliveredAt.Should().BeNull();
        result.Value.DiscardedAt.Should().BeNull();
        result.Value.DeliveryLink.Should().Be(inviteLink.AbsoluteUri);
    }

    [Fact]
    public void MarkDelivered_ShouldCompleteMessageAndRemoveSecretLink()
    {
        // Arrange: создаём письмо, которое фоновый обработчик уже зарезервировал.
        EmailOutboxMessage message = CreatePasswordResetMessage();
        DateTime processingStartedAt = DateTime.UtcNow;
        message.MarkProcessing(processingStartedAt).IsSuccess.Should().BeTrue();

        // Act: имитируем успешный ответ SMTP provider.
        DateTime deliveredAt = processingStartedAt.AddSeconds(1);
        var result = message.MarkDelivered(deliveredAt);

        // Assert: письмо завершено, а ссылка с raw token больше не хранится в outbox.
        result.IsSuccess.Should().BeTrue();
        message.DeliveredAt.Should().Be(deliveredAt);
        message.AttemptCount.Should().Be(1);
        message.ProcessingStartedAt.Should().BeNull();
        message.DeliveryLink.Should().BeNull();
        message.LastFailureCode.Should().BeNull();
    }

    [Fact]
    public void RegisterFailure_BeforeLastAttempt_ShouldScheduleNextAttemptAndKeepDeliveryPayload()
    {
        // Arrange: задаём время следующей попытки так же, как processor после ошибки SMTP.
        EmailOutboxMessage message = CreatePasswordResetMessage();
        DateTime failedAt = DateTime.UtcNow;
        DateTime nextAttemptAt = failedAt.AddSeconds(30);
        message.MarkProcessing(failedAt).IsSuccess.Should().BeTrue();

        // Act: первая ошибка при лимите в пять попыток ещё не завершает сообщение.
        var result = message.RegisterFailure(failedAt, nextAttemptAt, maxAttempts: 5);

        // Assert: retry запланирован, ссылка сохранена только потому, что она нужна следующей попытке.
        result.IsSuccess.Should().BeTrue();
        message.AttemptCount.Should().Be(1);
        message.NextAttemptAt.Should().Be(nextAttemptAt);
        message.DiscardedAt.Should().BeNull();
        message.DeliveryLink.Should().NotBeNull();
        message.LastFailureCode.Should().Be(EmailOutboxMessage.DELIVERY_FAILURE_CODE);
    }

    [Fact]
    public void RegisterFailure_OnLastAttempt_ShouldDiscardMessageAndRemoveSecretLink()
    {
        // Arrange: четыре предыдущие ошибки подводят сообщение к последней разрешённой попытке.
        EmailOutboxMessage message = CreatePasswordResetMessage();
        DateTime failedAt = DateTime.UtcNow;
        for (int attempt = 1; attempt < 5; attempt++)
        {
            DateTime attemptTime = failedAt.AddMinutes(attempt);
            message.MarkProcessing(attemptTime).IsSuccess.Should().BeTrue();
            message.RegisterFailure(attemptTime, attemptTime.AddMinutes(1), maxAttempts: 5)
                .IsSuccess.Should().BeTrue();
        }

        // Act: пятая ошибка исчерпывает retry policy.
        DateTime finalFailureAt = failedAt.AddMinutes(5);
        message.MarkProcessing(finalFailureAt).IsSuccess.Should().BeTrue();
        var result = message.RegisterFailure(finalFailureAt, finalFailureAt.AddMinutes(1), maxAttempts: 5);

        // Assert: запись остаётся для диагностики, но больше не содержит ссылку с raw token.
        result.IsSuccess.Should().BeTrue();
        message.AttemptCount.Should().Be(5);
        message.DiscardedAt.Should().Be(finalFailureAt);
        message.DeliveryLink.Should().BeNull();
    }

    [Fact]
    public void MarkDelivered_WithoutReservation_ShouldRejectInvalidStateTransition()
    {
        // Arrange: письмо создано, но фоновый обработчик ещё не резервировал его для отправки.
        EmailOutboxMessage message = CreatePasswordResetMessage();

        // Act: пытаемся завершить доставку, которая фактически не начиналась.
        var result = message.MarkDelivered(DateTime.UtcNow);

        // Assert: entity отклоняет переход и не изменяет delivery state.
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("email.outbox.transition.invalid");
        message.DeliveredAt.Should().BeNull();
        message.AttemptCount.Should().Be(0);
        message.DeliveryLink.Should().NotBeNull();
    }

    [Fact]
    public void RegisterFailure_WithNextAttemptBeforeFailure_ShouldRejectInvalidRetrySchedule()
    {
        // Arrange: резервируем письмо и готовим ошибочное расписание retry в прошлом.
        EmailOutboxMessage message = CreatePasswordResetMessage();
        DateTime failedAt = DateTime.UtcNow;
        message.MarkProcessing(failedAt).IsSuccess.Should().BeTrue();

        // Act: следующая попытка ошибочно назначена раньше текущей ошибки.
        var result = message.RegisterFailure(
            failedAt,
            failedAt.AddSeconds(-1),
            maxAttempts: 5);

        // Assert: entity сохраняет исходное состояние и сообщает validation error.
        result.IsFailure.Should().BeTrue();
        message.AttemptCount.Should().Be(0);
        message.LastFailureCode.Should().BeNull();
    }

    private static EmailOutboxMessage CreatePasswordResetMessage()
    {
        return EmailOutboxMessage.CreatePasswordReset(
            Guid.NewGuid(),
            "user@example.com",
            "Test User",
            new Uri("https://app.example.com/reset-password?token=secret-token"),
            DateTime.UtcNow.AddHours(1)).Value;
    }
}
