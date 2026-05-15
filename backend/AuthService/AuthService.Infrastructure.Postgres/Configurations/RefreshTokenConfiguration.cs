using AuthService.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Postgres.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnName("id");

        builder.Property(token => token.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(IdentityFieldLimits.REFRESH_TOKEN_HASH_MAX_LENGTH)
            .IsRequired();

        builder.Property(token => token.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc',now())")
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(token => token.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(token => token.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(token => token.ReplacedByTokenId)
            .HasColumnName("replaced_by_token_id");

        builder.Property(token => token.CreatedByIp)
            .HasColumnName("created_by_ip")
            .HasMaxLength(IdentityFieldLimits.IP_ADDRESS_MAX_LENGTH);

        builder.Property(token => token.RevokedByIp)
            .HasColumnName("revoked_by_ip")
            .HasMaxLength(IdentityFieldLimits.IP_ADDRESS_MAX_LENGTH);

        builder.Property(token => token.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(IdentityFieldLimits.USER_AGENT_MAX_LENGTH);

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_refresh_tokens_token_hash");

        builder.HasIndex(token => new
            {
                token.UserId,
                token.RevokedAt,
                token.ExpiresAt
            })
            .HasDatabaseName("ix_refresh_tokens_user_active_lookup");

        builder.HasOne(token => token.User)
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(token => token.ReplacedByToken)
            .WithOne()
            .HasForeignKey<RefreshToken>(token => token.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
