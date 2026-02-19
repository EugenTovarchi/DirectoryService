using FileService.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace FileService.Web;

public static class MigrationExtensions
{
    public static async Task ApplyMigrations(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var dsDbContext = scope.ServiceProvider.GetRequiredService<FileServiceDbContext>();
        await dsDbContext.Database.MigrateAsync();

        app.Logger.LogInformation("All migrations applied successfully");
    }
}