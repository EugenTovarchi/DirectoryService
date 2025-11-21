using DirectoryService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DirectoryService.Infrastructure.Postgres.Configurations;

public class DepartmentLocationConfiguration : IEntityTypeConfiguration<DepartmentLocation>
{
    public void Configure(EntityTypeBuilder<DepartmentLocation> builder)
    {
        builder.ToTable("department_locations");

        builder.HasKey(dl => new { dl.DepartmentId, dl.LocationId });

        builder.Property(dl => dl.DepartmentId)
            .HasColumnName("department_id")
            .IsRequired();

        builder.Property(dl => dl.LocationId)
            .HasColumnName("location_id")
            .IsRequired();
    }
}
