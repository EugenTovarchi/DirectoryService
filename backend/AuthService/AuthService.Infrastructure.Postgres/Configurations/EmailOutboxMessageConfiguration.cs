using AuthService.Domain.EmailDelivery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Postgres.Configurations;

/// <summary>
/// Описывает таблицу email outbox и индекс, по которому Quartz job выбирает готовые письма.
/// </summary>
public sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("email_outbox_messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id).HasColumnName("id");

        builder.Property(message => message.Type)
            .HasColumnName("type")
            .HasMaxLength(EmailOutboxMessage.TYPE_MAX_LENGTH)
            .IsRequired();

        builder.Property(message => message.UserId).HasColumnName("user_id");

        builder.Property(message => message.Email)
            .HasColumnName("email")
            .HasMaxLength(EmailOutboxMessage.EMAIL_MAX_LENGTH)
            .IsRequired();

        builder.Property(message => message.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(EmailOutboxMessage.DISPLAY_NAME_MAX_LENGTH);

        builder.Property(message => message.DeliveryLink)
            .HasColumnName("delivery_link")
            .HasMaxLength(EmailOutboxMessage.DELIVERY_LINK_MAX_LENGTH);

        builder.Property(message => message.ExpiresAt).HasColumnName("expires_at");
        builder.Property(message => message.CreatedAt).HasColumnName("created_at");
        builder.Property(message => message.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(message => message.AttemptCount).HasColumnName("attempt_count");
        builder.Property(message => message.ProcessingStartedAt).HasColumnName("processing_started_at");
        builder.Property(message => message.DeliveredAt).HasColumnName("delivered_at");

        builder.Property(message => message.DiscardedAt).HasColumnName("discarded_at");
        builder.Property(message => message.LastFailureCode)
            .HasColumnName("last_failure_code")
            .HasMaxLength(EmailOutboxMessage.FAILURE_CODE_MAX_LENGTH);

        // Partial index не разрастается за счёт уже доставленных и окончательно отброшенных писем.
        builder.HasIndex(message => new { message.NextAttemptAt, message.CreatedAt })
            .HasDatabaseName("ix_email_outbox_messages_due")
            .HasFilter("delivered_at IS NULL AND discarded_at IS NULL");
    }
}
