using FileService.Domain.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileService.Infrastructure.Postgres.Configurations;

public class PreviewAssetsConfiguration : IEntityTypeConfiguration<PreviewAsset>
{
    public void Configure(EntityTypeBuilder<PreviewAsset> builder)
    {
        builder.ToTable("preview_assets");
    }
}