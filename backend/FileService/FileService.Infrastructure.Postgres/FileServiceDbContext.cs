using FileService.Domain.Assets;
using FileService.Infrastructure.Postgres.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FileService.Infrastructure.Postgres;

public class FileServiceDbContext : DbContext
{
    public FileServiceDbContext(DbContextOptions<FileServiceDbContext> options)
        : base(options)
    {
    }

    public static FileServiceDbContext Create(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FileServiceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.EnableSensitiveDataLogging();

        return new FileServiceDbContext(optionsBuilder.Options);
    }

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<VideoAsset> VideoAssets => Set<VideoAsset>();
    public DbSet<PreviewAsset> PreviewAssets => Set<PreviewAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new MediaAssetsConfiguration());
    }
}