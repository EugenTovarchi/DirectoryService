using AuthService.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Postgres.Configurations;

public class AuthUserConfiguration : IEntityTypeConfiguration<AuthUser>
{
    public void Configure(EntityTypeBuilder<AuthUser> builder)
    {
        builder.ToTable("auth_users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasColumnName("id");

        builder.OwnsOne(user => user.Email, email =>
        {
            email.Property(value => value.Value)
                .HasColumnName("email")
                .HasMaxLength(Email.MAX_LENGTH)
                .IsRequired();

            email.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ix_auth_users_email");
        });

        builder.OwnsOne(user => user.Username, username =>
        {
            username.Property(value => value.Value)
                .HasColumnName("username")
                .HasMaxLength(Username.MAX_LENGTH)
                .IsRequired();

            username.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ix_auth_users_username");
        });

        builder.OwnsOne(user => user.PasswordHash, passwordHash =>
        {
            passwordHash.Property(value => value.Value)
                .HasColumnName("password_hash")
                .HasMaxLength(PasswordHash.MAX_LENGTH)
                .IsRequired();
        });

        builder.Property(user => user.CreatedAt)
            .HasDefaultValueSql("timezone('utc',now())")
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .HasDefaultValueSql("timezone('utc',now())")
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
