using FileService.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedService.Core.Abstractions;

namespace FileService.Web.Configurations;

/// <summary>
/// Создаёт FileServiceDbContext только для команд dotnet-ef.
/// Это позволяет EF не запускать Web host и фоновые сервисы во время работы с миграциями.
/// </summary>
public sealed class FileServiceDbContextFactory : IDesignTimeDbContextFactory<FileServiceDbContext>
{
    public FileServiceDbContext CreateDbContext(string[] args)
    {
        string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environments.Development;

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString(Constants.DEFAULT_CONNECTION)
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing or empty");

        var optionsBuilder = new DbContextOptionsBuilder<FileServiceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FileServiceDbContext(optionsBuilder.Options);
    }
}
