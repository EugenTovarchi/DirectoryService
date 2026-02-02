using DirectoryService.Application.Database;
using DirectoryService.Core.Abstractions;
using DirectoryService.Infrastructure.Postgres.BackgroundServices;
using DirectoryService.Infrastructure.Postgres.Database;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.Infrastructure.Postgres.Repositories;
using DirectoryService.Infrastructure.Postgres.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransactionManager = DirectoryService.Infrastructure.Postgres.Database.TransactionManager;

namespace DirectoryService.Infrastructure.Postgres;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddDirectoryServiceInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddDatabase(configuration)
            .AddRepositories()
            .AddServices();

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextPool<DirectoryServiceDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString(Constants.DEFAULT_CONNECTION);
            var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            options.UseNpgsql(connectionString);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is missing or empty");
            }

            options.LogTo(message =>
            {
                if (message.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("Exception", StringComparison.OrdinalIgnoreCase)) Console.WriteLine($"[EF] {message}");
            }, LogLevel.Error);

            if (hostEnvironment.IsDevelopment())
            {
                options.EnableDetailedErrors();
            }

            // options.UseLoggerFactory(loggerFactory);
        });

        services.AddScoped<ITransactionManager, TransactionManager>();
        services.AddScoped<TransactionManager>();
        services.AddScoped<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<NpgsqlConnectionFactory>();

        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<DepartmentRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<DeleteExpiredDepartmentsService>();
        services.AddHostedService<DeleteExpireDepartmentsBackgroundService>();

        return services;
    }
}
