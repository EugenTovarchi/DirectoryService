using AuthService.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Postgres.Configurations;

public sealed class AuthAuditEventConfiguration : IEntityTypeConfiguration<AuthAuditEvent>
{
    public void Configure(EntityTypeBuilder<AuthAuditEvent> builder)
    {
        builder.ToTable("auth_audit_events");

        builder.HasKey(auditEvent => auditEvent.Id);

        builder.Property(auditEvent => auditEvent.Id)
            .HasColumnName("id");

        builder.Property(auditEvent => auditEvent.CompanyId)
            .HasColumnName("company_id");

        builder.Property(auditEvent => auditEvent.UserId)
            .HasColumnName("user_id");

        builder.Property(auditEvent => auditEvent.Email)
            .HasColumnName("email")
            .HasMaxLength(AuthAuditEvent.EMAIL_MAX_LENGTH);

        builder.Property(auditEvent => auditEvent.Action)
            .HasColumnName("action")
            .HasMaxLength(AuthAuditEvent.ACTION_MAX_LENGTH)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.ActorUserId)
            .HasColumnName("actor_user_id");

        builder.Property(auditEvent => auditEvent.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc',now())")
            .IsRequired();

        builder.Property(auditEvent => auditEvent.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(AuthAuditEvent.IP_ADDRESS_MAX_LENGTH);

        builder.Property(auditEvent => auditEvent.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(AuthAuditEvent.USER_AGENT_MAX_LENGTH);

        builder.Property(auditEvent => auditEvent.MetadataJson)
            .HasColumnName("metadata_json")
            .HasColumnType("jsonb");

        builder.HasIndex(auditEvent => new
            {
                auditEvent.CompanyId,
                auditEvent.CreatedAt
            })
            .HasDatabaseName("ix_auth_audit_events_company_created_at");

        builder.HasIndex(auditEvent => new
            {
                auditEvent.UserId,
                auditEvent.CreatedAt
            })
            .HasDatabaseName("ix_auth_audit_events_user_created_at");

        builder.HasIndex(auditEvent => new
            {
                auditEvent.Action,
                auditEvent.CreatedAt
            })
            .HasDatabaseName("ix_auth_audit_events_action_created_at");
    }
}
