using AuthService.Core.Options;
using AuthService.Infrastructure.Postgres;
using AuthService.Infrastructure.Postgres.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Web;

public static class MigrationExtensions
{
    public static async Task ApplyMigrationsIfNeeded(this WebApplication app)
    {
        if (app.Environment.IsEnvironment("Testing"))
            return;

        await app.ApplyMigrations();
    }

    public static async Task ApplyMigrations(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AuthServiceDbContext>();
        await dbContext.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<AuthIdentitySeeder>();
        await seeder.SeedAsync();

        OidcServerOptions oidcOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<OidcServerOptions>>()
            .Value;

        if (oidcOptions.Enabled)
        {
            var oidcSeeder = scope.ServiceProvider.GetRequiredService<OidcServerSeeder>();
            await oidcSeeder.SeedAsync();
        }

        app.Logger.LogInformation("All migrations applied successfully");
    }
}
