using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeZone = DirectoryService.Contracts.ValueObjects.TimeZone;

namespace DirectoryService.Infrastructure.Postgres.Configurations;

public static class LocationConstants
{
    public const int MAX_LENGTH = 50;
}

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

        builder.OwnsOne(l => l.Address, address =>
        {
            address.Property(a => a.Country)
                .HasColumnName("country")
                .HasMaxLength(100)
                .IsRequired();

            address.Property(a => a.City)
                .HasColumnName("city")
                .HasMaxLength(100)
                .IsRequired();

            address.Property(a => a.Street)
                .HasColumnName("street")
                .HasMaxLength(200)
                .IsRequired();

            address.Property(a => a.House)
                .HasColumnName("house")
                .HasMaxLength(20)
                .IsRequired();

            address.Property(a => a.Flat)
                .HasColumnName("flat")
                .IsRequired(false);
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
