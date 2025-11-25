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
    DbSet<Department> Departments => Set<Department>();
    DbSet<Location> Locations => Set<Location>();
    DbSet<Position> Positions => Set<Position>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableSensitiveDataLogging();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new DepartmentConfiguration());
        modelBuilder.ApplyConfiguration(new LocationConfiguration());
        modelBuilder.ApplyConfiguration(new PositionConfiguration());
        modelBuilder.ApplyConfiguration(new DepartmentLocationConfiguration());
        modelBuilder.ApplyConfiguration(new DepartmentPositionConfiguration());
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
    }
}
