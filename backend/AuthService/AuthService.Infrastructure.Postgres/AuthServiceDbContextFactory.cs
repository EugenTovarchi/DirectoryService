using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuthService.Infrastructure.Postgres;

/// <summary>
/// Design-time factory для EF tools: миграции можно создавать без запуска Web host и без реального подключения к БД.
/// </summary>
public sealed class AuthServiceDbContextFactory : IDesignTimeDbContextFactory<AuthServiceDbContext>
{
    public AuthServiceDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=auth_service_design_time;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AuthServiceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AuthServiceDbContext(optionsBuilder.Options);
    }
}
