using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel.ValueObjects.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DirectoryService.Infrastructure.Postgres.Configurations;

public class DepartmentPositionConfiguration : IEntityTypeConfiguration<DepartmentPosition>
{
    public void Configure(EntityTypeBuilder<DepartmentPosition> builder)
    {
        builder.ToTable("department_positions");

        builder.HasKey(dp => new { dp.DepartmentId, dp.PositionId });

        builder.Property(dp => dp.DepartmentId)
            .HasConversion(
                id => id.Value,
                value => DepartmentId.Create(value))
            .HasColumnName("department_id")
            .IsRequired();

        builder.Property(dp => dp.PositionId)
            .HasConversion(
                id => id.Value,
                value => PositionId.Create(value))
            .HasColumnName("position_id")
            .IsRequired();
    }
}
