using FileService.Core.FilesStorage;
using FileService.Domain.Assets;
using FileService.Infrastructure.Postgres.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FileService.Infrastructure.Postgres;

public class FileServiceDbContext : DbContext, IFileReadDbContext
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

    public IQueryable<MediaAsset> ReadMediaAssets => MediaAssets.AsQueryable().AsNoTracking();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new MediaAssetsConfiguration());
        modelBuilder.ApplyConfiguration(new VideoProcessesConfiguration());
    }
}