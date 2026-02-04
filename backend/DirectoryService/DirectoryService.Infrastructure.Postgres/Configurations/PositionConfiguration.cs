using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DirectoryService.Infrastructure.Postgres.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("positions");

        builder.HasKey(x => x.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => PositionId.Create(value))
            .HasColumnName("id");

        builder.OwnsOne(p => p.Name, name =>
        {
            name.Property(n => n.Value)
                .HasColumnName("name")
                .HasMaxLength(Name.MAX_LENGTH)
                .IsRequired();
        });

        builder.OwnsOne(p => p.Description, desc =>
        {
            desc.Property(d => d.Value)
                .HasColumnName("description")
                .HasMaxLength(Description.MAX_LENGTH)
                .HasDefaultValue(string.Empty)
                .IsRequired(false);
        });

        builder.HasMany(p => p.DepartmentPositions)
            .WithOne()
            .HasForeignKey(dp => dp.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at")
            .IsRequired(false);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("timezone('utc',now())")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasDefaultValueSql("timezone('utc',now())")
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
