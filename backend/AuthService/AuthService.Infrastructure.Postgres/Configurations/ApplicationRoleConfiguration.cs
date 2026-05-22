using AuthService.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Postgres.Configurations;

public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("identity_roles");

        builder.Property(role => role.Id)
            .HasColumnName("id");

        builder.Property(role => role.Name)
            .HasColumnName("name")
            .HasMaxLength(IdentityFieldLimits.ROLE_NAME_MAX_LENGTH);

        builder.Property(role => role.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(IdentityFieldLimits.ROLE_NAME_MAX_LENGTH);

        builder.Property(role => role.Description)
            .HasColumnName("description")
            .HasMaxLength(IdentityFieldLimits.ROLE_DESCRIPTION_MAX_LENGTH);

        builder.Property(role => role.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc',now())")
            .IsRequired();

        builder.Property(role => role.ConcurrencyStamp)
            .HasColumnName("concurrency_stamp");

        builder.HasIndex(role => role.NormalizedName)
            .IsUnique()
            .HasDatabaseName("ix_identity_roles_normalized_name");
    }
}
