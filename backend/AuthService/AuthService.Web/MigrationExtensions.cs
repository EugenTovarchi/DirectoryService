using AuthService.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

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

        app.Logger.LogInformation("All migrations applied successfully");
    }
}
