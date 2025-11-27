using DirectoryService.Application;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DirectoryService.Infrastructure.Postgres;

public static class DependencyInjection
{
    public static IServiceCollection AddDirectoryServiceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddDatabase(configuration)
            .AddRepositories();

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

            if (hostEnvironment.IsDevelopment())
            {
                options.EnableDetailedErrors();
            }

            options.UseLoggerFactory(loggerFactory);
        });

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<ILocationRepository, LocationRepository>();

        return services;
    }
}
