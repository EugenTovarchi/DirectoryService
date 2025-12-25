using DirectoryService.Domain.Entities;
using DirectoryService.Infrastructure.Postgres.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DirectoryService.Infrastructure.Postgres.DbContexts;

public class DirectoryServiceDbContext : DbContext
{
    public DirectoryServiceDbContext(DbContextOptions<DirectoryServiceDbContext> options)
        : base(options)
    {
    }
    public DirectoryServiceDbContext(string connectionString)
       : base(CreateOptions(connectionString))
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<DepartmentPosition> DepartmentPositions => Set<DepartmentPosition>();
    public DbSet<DepartmentLocation> DepartmentLocations => Set<DepartmentLocation>();

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //{
    //    optionsBuilder.EnableSensitiveDataLogging();
    //}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("ltree");
        modelBuilder.ApplyConfiguration(new DepartmentConfiguration());
        modelBuilder.ApplyConfiguration(new LocationConfiguration());
        modelBuilder.ApplyConfiguration(new PositionConfiguration());
        modelBuilder.ApplyConfiguration(new DepartmentLocationConfiguration());
        modelBuilder.ApplyConfiguration(new DepartmentPositionConfiguration());
        //AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
    }
    private static DbContextOptions<DirectoryServiceDbContext> CreateOptions(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DirectoryServiceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.EnableSensitiveDataLogging();
        return optionsBuilder.Options;
    }
}
