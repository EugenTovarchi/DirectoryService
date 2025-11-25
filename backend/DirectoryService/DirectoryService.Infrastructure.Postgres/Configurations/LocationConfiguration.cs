using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel.ValueObjects;
using DirectoryService.SharedKernel.ValueObjects.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeZone = DirectoryService.SharedKernel.ValueObjects.TimeZone;

namespace DirectoryService.Infrastructure.Postgres.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");

        builder.HasKey(x => x.Id);

        builder.Property(l => l.Id)
            .HasConversion(
                id => id.Value,
                value => LocationId.Create(value))
            .HasColumnName("id");

        builder.OwnsOne(l => l.Name, name =>
        {
            name.Property(n => n.Value)
                .HasColumnName("name")
                .HasMaxLength(Name.MAX_LENGTH)
                .IsRequired();
        });

        builder.ComplexProperty(l => l.Address, address =>
        {
            address.Property(a => a.City).HasColumnName("city").IsRequired();
            address.Property(a => a.Street).HasColumnName("street").IsRequired();
            address.Property(a => a.House).HasColumnName("house").IsRequired();
            address.Property(a => a.Flat).HasColumnName("flat");
        });

        builder.OwnsOne(l => l.TimeZone, timeZone =>
        {
            timeZone.Property(tz => tz.Value)
                .HasColumnName("time_zone")
                .HasMaxLength(TimeZone.MAX_LENGTH)
                .IsRequired();
        });

        builder.HasMany(l => l.DepartmentLocations)
            .WithOne()
            .HasForeignKey(dl => dl.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(l => l.DeletionDate)
            .HasColumnName("deletion_date")
            .IsRequired(false);

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}

