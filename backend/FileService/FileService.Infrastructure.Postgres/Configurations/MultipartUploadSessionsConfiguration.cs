using FileService.Domain.Assets;
using FileService.Domain.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileService.Infrastructure.Postgres.Configurations;

public sealed class MultipartUploadSessionsConfiguration : IEntityTypeConfiguration<MultipartUploadSession>
{
    public void Configure(EntityTypeBuilder<MultipartUploadSession> builder)
    {
        builder.ToTable("multipart_upload_sessions");
        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id).HasColumnName("id");
        builder.Property(session => session.MediaAssetId).HasColumnName("media_asset_id");
        builder.Property(session => session.UploadId)
            .HasColumnName("upload_id")
            .HasMaxLength(MultipartUploadSession.MAX_UPLOAD_ID_LENGTH);
        builder.Property(session => session.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(MultipartUploadSession.MAX_IDEMPOTENCY_KEY_LENGTH);
        builder.Property(session => session.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();
        builder.Property(session => session.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(session => session.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(session => session.ExpiresAt).HasColumnName("expires_at");
        builder.Property(session => session.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(session => session.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(MultipartUploadSession.MAX_FAILURE_REASON_LENGTH);

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(session => session.MediaAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        // Один MediaAsset представляет одну попытку загрузки. Повторный клиентский запрос
        // должен переиспользовать эту сессию, а не открывать второй multipart upload.
        builder.HasIndex(session => session.MediaAssetId)
            .IsUnique()
            .HasDatabaseName("ux_multipart_upload_sessions_media_asset_id");

        builder.HasIndex(session => session.UploadId)
            .IsUnique()
            .HasFilter("upload_id IS NOT NULL")
            .HasDatabaseName("ux_multipart_upload_sessions_upload_id");

        builder.HasIndex(session => session.IdempotencyKey)
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ux_multipart_upload_sessions_idempotency_key");

        builder.HasIndex(session => new { session.Status, session.ExpiresAt })
            .HasDatabaseName("ix_multipart_upload_sessions_status_expires_at");
    }
}