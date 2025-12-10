using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel.ValueObjects;
using DirectoryService.SharedKernel.ValueObjects.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Path = DirectoryService.SharedKernel.ValueObjects.Path;

namespace DirectoryService.Infrastructure.Postgres.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");

        builder.HasKey(x => x.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasConversion(
            id => id.Value,
            value => DepartmentId.Create(value));

        builder.OwnsOne(d => d.Name, name =>
        {
            name.Property(name => name.Value)
           .HasColumnName("name")
           .HasMaxLength(Name.MAX_LENGTH)
           .IsRequired();
        });

        builder.OwnsOne(d => d.Identifier, ident =>
        {
            ident.Property(ident => ident.Value)
           .HasColumnName("identifier")
           .HasMaxLength(Identifier.MAX_LENGTH)
           .IsRequired();
        });

        builder.Property(d => d.Path)
            .HasConversion(
            value => value.Value,
            value => Path.Create(value).Value)
            .HasColumnName("path")
            .HasMaxLength(Path.MAX_LENGTH)
            .HasColumnType("ltree")
            .IsRequired();

        builder.Property(d => d.Depth)
            .HasColumnName("depth")
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(d => d.ParentId)
        .HasColumnName("parent_id")
        .IsRequired(false);

        builder.HasMany(d => d.ChildrenDepartment)
            .WithOne()
            .IsRequired(false)
            .HasForeignKey(d => d.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.DepartmentLocations)
            .WithOne()
            .HasForeignKey(dl => dl.DepartmentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.DepartmentPositions)
            .WithOne()
            .HasForeignKey(dl => dl.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(d => d.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(d => d.DeletionDate)
            .HasColumnName("deletion_date")
            .IsRequired(false);

        builder.Property(d => d.CreatedAt)
            .HasDefaultValueSql("timezone('utc',now())")
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasDefaultValueSql("timezone('utc',now())")
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasQueryFilter(d => !d.IsDeleted);

        builder.HasIndex(d => d.Path).HasMethod("gist").HasDatabaseName("idx_departments_path");
    }
}
