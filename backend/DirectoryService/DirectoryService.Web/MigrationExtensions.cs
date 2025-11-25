using DirectoryService.Infrastructure.Postgres.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace DirectoryService.Web;

public static class MigrationExtensions
{
    public async static Task ApplyMigrations(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var dsDbContext = scope.ServiceProvider.GetRequiredService<DirectoryServiceDbContext>();
        await dsDbContext.Database.MigrateAsync();

        app.Logger.LogInformation("All migrations applied successfully");
    }
}

