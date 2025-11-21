using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel.ValueObjects;
using DirectoryService.SharedKernel.ValueObjects.Ids;
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
                .IsRequired(false); 
        });

        builder.HasMany(p => p.DepartmentPositions)
            .WithOne() 
            .HasForeignKey(dp => dp.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(p => p.DeletionDate)
            .HasColumnName("deletion_date")
            .IsRequired(false);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
