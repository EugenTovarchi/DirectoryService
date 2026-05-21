using AuthService.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Postgres.Configurations;

public sealed class UserInviteTokenConfiguration : IEntityTypeConfiguration<UserInviteToken>
{
    public void Configure(EntityTypeBuilder<UserInviteToken> builder)
    {
        builder.ToTable("user_invite_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnName("id");

        builder.Property(token => token.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(token => token.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(UserInviteToken.TOKEN_HASH_LENGTH)
            .IsRequired();

        builder.Property(token => token.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc',now())")
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(token => token.AcceptedAt)
            .HasColumnName("accepted_at");

        builder.Property(token => token.RevokedAt)
            .HasColumnName("revoked_at");

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_user_invite_tokens_token_hash");

        builder.HasIndex(token => token.CreatedByUserId)
            .HasDatabaseName("ix_user_invite_tokens_created_by_user_id");

        builder.HasIndex(token => new
            {
                token.UserId,
                token.AcceptedAt,
                token.RevokedAt,
                token.ExpiresAt
            })
            .HasDatabaseName("ix_user_invite_tokens_user_active_lookup");

        builder.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(token => token.CreatedByUser)
            .WithMany()
            .HasForeignKey(token => token.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
