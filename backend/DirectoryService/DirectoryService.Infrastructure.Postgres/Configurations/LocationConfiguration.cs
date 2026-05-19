using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeZone = DirectoryService.Contracts.ValueObjects.TimeZone;

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

            name.HasIndex(n => n.Value)
                .IsUnique()
                .HasDatabaseName("ix_location_name");
        });

        builder.OwnsOne(l => l.Address, address =>
        {
            address.Property(a => a.Country)
                .HasColumnName("country")
                .HasMaxLength(Address.COUNTRY_MAX_LENGTH)
                .IsRequired();

            address.Property(a => a.City)
                .HasColumnName("city")
                .HasMaxLength(Address.CITY_MAX_LENGTH)
                .IsRequired();

            address.Property(a => a.Street)
                .HasColumnName("street")
                .HasMaxLength(Address.STREET_MAX_LENGTH)
                .IsRequired();

            address.Property(a => a.House)
                .HasColumnName("house")
                .HasMaxLength(Address.HOUSE_MAX_LENGTH)
                .IsRequired();

            address.Property(a => a.Flat)
                .HasColumnName("flat")
                .IsRequired(false);

            address.HasIndex(a => new { a.Country, a.City, a.Street, a.House, a.Flat })
                .HasFilter("flat IS NOT NULL")
                .HasDatabaseName("ix_location_with_flat");

            address.HasIndex(a => new { a.Country, a.City, a.Street, a.House })
                .HasFilter("flat IS NULL")
                .HasDatabaseName("ix_location_without_flat");
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

        builder.Property(l => l.DeletedAt)
            .HasColumnName("deleted_at")
            .IsRequired(false);

        builder.Property(l => l.CreatedAt)
            .HasDefaultValueSql("timezone('utc',now())")
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(l => l.UpdatedAt)
            .HasDefaultValueSql("timezone('utc',now())")
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
