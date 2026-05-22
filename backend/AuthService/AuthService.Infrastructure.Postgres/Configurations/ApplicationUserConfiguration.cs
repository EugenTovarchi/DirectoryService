using AuthService.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Postgres.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("identity_users");

        builder.Property(user => user.Id)
            .HasColumnName("id");

        builder.Property(user => user.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(Username.MAX_LENGTH);

        builder.Property(user => user.NormalizedUserName)
            .HasColumnName("normalized_user_name")
            .HasMaxLength(Username.MAX_LENGTH);

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(IdentityFieldLimits.EMAIL_MAX_LENGTH);

        builder.Property(user => user.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(IdentityFieldLimits.EMAIL_MAX_LENGTH);

        builder.OwnsOne(user => user.DisplayName, displayName =>
        {
            displayName.Property(value => value.Value)
                .HasColumnName("display_name")
                .HasMaxLength(DisplayName.MAX_LENGTH);
        });

        builder.Property(user => user.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(user => user.CurrentCompanyId)
            .HasColumnName("current_company_id");

        builder.Property(user => user.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc',now())")
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("timezone('utc',now())")
            .IsRequired();

        builder.Property(user => user.EmailConfirmed).HasColumnName("email_confirmed");
        builder.Property(user => user.PasswordHash).HasColumnName("password_hash");
        builder.Property(user => user.SecurityStamp).HasColumnName("security_stamp");
        builder.Property(user => user.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        builder.Property(user => user.PhoneNumber).HasColumnName("phone_number");
        builder.Property(user => user.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
        builder.Property(user => user.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        builder.Property(user => user.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(user => user.LockoutEnabled).HasColumnName("lockout_enabled");
        builder.Property(user => user.AccessFailedCount).HasColumnName("access_failed_count");

        builder.HasIndex(user => user.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("ix_identity_users_normalized_user_name");

        builder.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("ix_identity_users_normalized_email");
    }
}
