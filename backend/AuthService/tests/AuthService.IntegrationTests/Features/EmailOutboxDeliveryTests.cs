using AuthService.Domain.EmailDelivery;
using AuthService.Infrastructure.Postgres;
using AuthService.Infrastructure.Postgres.EmailDelivery;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.IntegrationTests.Features;

/// <summary>
/// Проверяет reusable outbox flow через реальную PostgreSQL и управляемый fake email sender.
/// Quartz timer здесь не нужен: тест явно запускает тот же processor, который вызывает Quartz job.
/// </summary>
public sealed class EmailOutboxDeliveryTests : AuthServiceBaseTests
{
    public EmailOutboxDeliveryTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task DeliveryFailure_ShouldPersistRetryState_AndNextAttemptShouldDeliverMessage()
    {
        // Arrange: сохраняем готовое письмо и просим fake sender вернуть одну контролируемую ошибку.
        Guid userId = Guid.NewGuid();
        EmailOutboxMessage message = EmailOutboxMessage.CreateInvite(
            userId,
            "outbox-retry@example.com",
            "Outbox Retry User",
            new Uri("https://app.example.com/invite?token=retry-token"),
            DateTime.UtcNow.AddDays(3)).Value;
        await SaveOutboxMessageAsync(message);

        TestInviteEmailSender emailSender = Services.GetRequiredService<TestInviteEmailSender>();
        emailSender.FailNextAttempts(1);

        // Act 1: первая попытка завершается ошибкой SMTP.
        await ProcessEmailOutboxAsync();

        // Assert 1: запись остаётся готовой к retry, а secret-ссылка пока нужна следующей попытке.
        EmailOutboxMessage failedMessage = await LoadOutboxMessageAsync(message.Id);
        failedMessage.AttemptCount.Should().Be(1);
        failedMessage.DeliveredAt.Should().BeNull();
        failedMessage.DiscardedAt.Should().BeNull();
        failedMessage.DeliveryLink.Should().NotBeNull();
        failedMessage.LastFailureCode.Should().Be(EmailOutboxMessage.DELIVERY_FAILURE_CODE);

        // Act 2: ждём короткий testing backoff и повторяем тот же проход processor.
        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        await ProcessEmailOutboxAsync();

        // Assert 2: вторая попытка доставила письмо, завершила запись и удалила ссылку с raw token.
        EmailOutboxMessage deliveredMessage = await LoadOutboxMessageAsync(message.Id);
        deliveredMessage.AttemptCount.Should().Be(2);
        deliveredMessage.DeliveredAt.Should().NotBeNull();
        deliveredMessage.DeliveryLink.Should().BeNull();
        emailSender.Messages.Should().Contain(email => email.UserId == userId);
    }

    private async Task SaveOutboxMessageAsync(EmailOutboxMessage message)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AuthServiceDbContext dbContext = scope.ServiceProvider.GetRequiredService<AuthServiceDbContext>();
        dbContext.EmailOutboxMessages.Add(message);
        await dbContext.SaveChangesAsync();
    }

    private Task<EmailOutboxMessage> LoadOutboxMessageAsync(Guid messageId)
    {
        return ExecuteInDb(dbContext => dbContext.EmailOutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == messageId));
    }

    private async Task ProcessEmailOutboxAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        EmailOutboxProcessor processor = scope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>();
        await processor.ProcessBatchAsync();
    }
}
