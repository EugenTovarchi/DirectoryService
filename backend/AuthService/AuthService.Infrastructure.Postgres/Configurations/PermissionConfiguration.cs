using AuthService.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Postgres.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("identity_permissions");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Id)
            .HasColumnName("id");

        builder.Property(permission => permission.Code)
            .HasColumnName("code")
            .HasMaxLength(IdentityFieldLimits.PERMISSION_CODE_MAX_LENGTH)
            .IsRequired();

        builder.Property(permission => permission.Description)
            .HasColumnName("description")
            .HasMaxLength(IdentityFieldLimits.PERMISSION_DESCRIPTION_MAX_LENGTH);

        builder.Property(permission => permission.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc',now())")
            .IsRequired();

        builder.HasIndex(permission => permission.Code)
            .IsUnique()
            .HasDatabaseName("ix_identity_permissions_code");
    }
}
