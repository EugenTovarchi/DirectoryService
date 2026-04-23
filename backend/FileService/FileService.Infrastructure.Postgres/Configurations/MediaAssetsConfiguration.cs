using System.Text.Json;
using FileService.Domain;
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
                    .HasColumnName("file_full_name")
                    .HasMaxLength(500)
                    .IsRequired();

                fileName.Property(f => f.Name)
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

        builder.Property(m => m.RawKey)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<StorageKey>(v, (JsonSerializerOptions?)null))
                    .HasColumnName("raw_key")
                    .HasColumnType("jsonb");

        builder.Property(m => m.Key)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<StorageKey>(v, (JsonSerializerOptions?)null))
                    .HasColumnName("key")
                    .HasColumnType("jsonb");

        builder.HasDiscriminator(m => m.AssetType)
            .HasValue<VideoAsset>(AssetType.VIDEO)
            .HasValue<PreviewAsset>(AssetType.PREVIEW)
            .HasValue<PhotoAsset>(AssetType.PHOTO);

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();
    }
}