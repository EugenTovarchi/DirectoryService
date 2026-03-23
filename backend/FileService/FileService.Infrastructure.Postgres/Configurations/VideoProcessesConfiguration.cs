using System.Text.Json;
using FileService.Domain;
using FileService.Domain.MediaProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileService.Infrastructure.Postgres.Configurations;

public class VideoProcessesConfiguration : IEntityTypeConfiguration<VideoProcess>
{
    public void Configure(EntityTypeBuilder<VideoProcess> builder)
    {
        builder.ToTable("video_processes");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id");

        builder.Property(m => m.RawKey)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<StorageKey>(v, (JsonSerializerOptions?)null)!)
            .HasColumnName("raw_key")
            .HasColumnType("jsonb");

        builder.Property(m => m.HlsKey)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<StorageKey>(v, (JsonSerializerOptions?)null))
            .HasColumnName("hls_key")
            .HasColumnType("jsonb");

        builder.Property(v => v.Status).HasConversion<string>().HasColumnName("status");
        builder.Property(v => v.TotalProgress).HasColumnName("total_progress");
        builder.Property(v => v.ErrorMessage).HasColumnName("error_message");
        builder.Property(v => v.CreatedAt).HasColumnName("started_at");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");

        builder.OwnsMany(vp => vp.Steps, sb =>
        {
            sb.ToTable("video_processing_steps");
            sb.HasKey(s => s.Id);

            sb.Property(ps => ps.Id).HasColumnName("id");
            sb.Property(ps => ps.Name).HasConversion<string>().HasColumnName("name");
            sb.Property(ps => ps.Order).HasColumnName("order");
            sb.Property(ps => ps.Progress).HasColumnName("progress");
            sb.Property(ps => ps.Status).HasConversion<string>().HasColumnName("status");
            sb.Property(ps => ps.ErrorMessage).HasColumnName("error_message");
            sb.Property(ps => ps.StartedAt).HasColumnName("started_at");
            sb.Property(ps => ps.CompletedAt).HasColumnName("completed_at");
            sb.Property(ps => ps.UpdatedAt).HasColumnName("updated_at");
            sb.Property(ps => ps.CreatedAt).HasColumnName("created_at");

            sb.WithOwner().HasForeignKey("VideoProcessId");
            sb.Property<Guid>("VideoProcessId").HasColumnName("video_process_id");

        });

        builder.OwnsOne(vp => vp.MetaData, metaData =>
        {
            metaData.Property(md => md.Duration).HasColumnName("duration");
            metaData.Property(md => md.Width).HasColumnName("width");
            metaData.Property(md => md.Height).HasColumnName("height");
            metaData.Property(md => md.FrameRate).HasColumnName("frame_rate");
            metaData.Property(md => md.Codec).HasColumnName("codec");
            metaData.Property(md => md.Bitrate).HasColumnName("bitrate");
        });

        builder.HasIndex(ps => new { ps.CreatedAt }).HasDatabaseName("ix_processes_created_at");
        builder.HasIndex(ps => new { ps.Status }).HasDatabaseName("ix_processes_steps_step_status");
    }
}