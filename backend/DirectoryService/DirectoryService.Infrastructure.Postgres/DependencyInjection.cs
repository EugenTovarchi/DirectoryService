using DirectoryService.Application;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryService.Infrastructure.Postgres;

public static class DependencyInjection
{
    public static IServiceCollection AddDirectoryServiceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddDatabase(configuration);

        return services;
    }
    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is missing or empty");
        }
        services.AddDbContext<DirectoryServiceDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ILocationRepository, LocationRepository>();

        return services;
    }
}
