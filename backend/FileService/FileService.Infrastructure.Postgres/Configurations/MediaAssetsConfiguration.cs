using FileService.Domain.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileService.Infrastructure.Postgres.Configurations;

public class MediaAssetsConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets");

        builder.HasKey(m => m.Id);

        builder.OwnsOne(m => m.MediaData, mediaData =>
        {
            mediaData.OwnsOne(md => md.FileName, fileName =>
            {
                fileName.Property(f => f.Value)
                    .HasColumnName("file_name")
                    .HasMaxLength(500)
                    .IsRequired();

                fileName.Property(f => f.Extension)
                    .HasColumnName("file_extension")
                    .HasMaxLength(10)
                    .IsRequired();
            });

            mediaData.OwnsOne(md => md.ContentType, contentType =>
            {
                contentType.Property(ct => ct.Value)
                    .HasColumnName("content_type")
                    .HasMaxLength(100)
                    .IsRequired();

                contentType.Property(ct => ct.MediaType)
                    .HasColumnName("media_type")
                    .HasConversion<string>()
                    .IsRequired();
            });

            mediaData.Property(md => md.Size)
                .HasColumnName("size")
                .HasColumnType("bigint")
                .IsRequired();

            mediaData.Property(md => md.ExpectedChunkCount)
                .HasColumnName("chunk_count")
                .HasColumnType("int");
        });

        builder.Property(m => m.AssetType)
            .HasColumnName("asset_type")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasDefaultValueSql("timezone('utc',now())")
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(m => m.UpdatedAt)
            .HasDefaultValueSql("timezone('utc',now())")
            .HasColumnName("updated_at")
            .IsRequired();

        builder.OwnsOne(m => m.Key, storageKey =>
        {
            storageKey.Property(mk => mk.Key)
                .HasColumnName("key")
                .HasMaxLength(500)
                .IsRequired();

            storageKey.Property(mk => mk.Prefix)
                .HasColumnName("prefix")
                .HasMaxLength(50)
                .IsRequired(false);

            storageKey.Property(mk => mk.Location)
                .HasColumnName("location")
                .HasMaxLength(500)
                .IsRequired();

            storageKey.Property(mk => mk.FullPath)
                .HasColumnName("full_path")
                .HasMaxLength(500)
                .IsRequired();
        });

        builder.UseTptMappingStrategy();

        builder.Property(m => m.Status).HasColumnName("status").HasConversion<string>().IsRequired();
    }
}