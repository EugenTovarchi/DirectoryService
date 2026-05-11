using AuthService.Domain.Users;
using AuthService.Infrastructure.Postgres.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Postgres;

public class AuthServiceDbContext : DbContext
{
    public AuthServiceDbContext(DbContextOptions<AuthServiceDbContext> options)
        : base(options)
    {
    }

    public static AuthServiceDbContext Create(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuthServiceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.EnableSensitiveDataLogging();

        return new AuthServiceDbContext(optionsBuilder.Options);
    }

    public DbSet<AuthUser> AuthUsers => Set<AuthUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new AuthUserConfiguration());
    }
}
