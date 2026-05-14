using FileService.Core.Abstractions;
using FileService.Core.FilesStorage;
using FileService.Infrastructure.Postgres.Database;
using FileService.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedService.Core.Abstractions;

namespace FileService.Infrastructure.Postgres;

public static class PostgresDependancyInjection
{
    public static void AddPostgresInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddReadDbContext()
            .AddDatabase(configuration);
    }

    private static void AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<NpgsqlDataSource>(_ =>
        {
            string? connectionString = configuration.GetConnectionString(Constants.DEFAULT_CONNECTION);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is missing or empty");
            }

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString)
            {
                Name = "file-service-db",
            };

            return dataSourceBuilder.Build();
        });

        services.AddDbContext<FileServiceDbContext>((sp, options) =>
        {
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
            var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
            sp.GetRequiredService<ILoggerFactory>();

            options.UseNpgsql(dataSource);

            options.LogTo(message =>
            {
                if (message.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("Exception", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[EF] {message}");
                }
            }, LogLevel.Error);

            if (hostEnvironment.IsDevelopment())
            {
                options.EnableDetailedErrors();
            }
        });

        // Репозитории
        services.AddScoped<IMediaAssetsRepository, MediaAssetsRepository>();
        services.AddScoped<IVideoProcessesRepository, VideoProcessesRepository>();
        services.AddScoped<ITransactionManager, TransactionManager>();
    }

    private static IServiceCollection AddReadDbContext(this IServiceCollection services)
    {
        services.AddScoped<IFileReadDbContext, FileServiceDbContext>();

        return services;
    }
}
