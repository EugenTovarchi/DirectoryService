using AuthService.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Postgres.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnName("id");

        builder.Property(token => token.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(PasswordResetToken.TOKEN_HASH_LENGTH)
            .IsRequired();

        builder.Property(token => token.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc',now())")
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(token => token.UsedAt)
            .HasColumnName("used_at");

        builder.Property(token => token.RevokedAt)
            .HasColumnName("revoked_at");

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_password_reset_tokens_token_hash");

        builder.HasIndex(token => new
            {
                token.UserId,
                token.UsedAt,
                token.RevokedAt,
                token.ExpiresAt
            })
            .HasDatabaseName("ix_password_reset_tokens_user_active_lookup");

        builder.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
