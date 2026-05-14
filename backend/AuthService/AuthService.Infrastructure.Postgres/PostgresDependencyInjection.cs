using AuthService.Core.Abstractions;
using AuthService.Core.Database;
using AuthService.Infrastructure.Postgres.Database;
using AuthService.Infrastructure.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using SharedService.Core.Abstractions;

namespace AuthService.Infrastructure.Postgres;

public static class PostgresDependencyInjection
{
    public static IServiceCollection AddPostgresInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddDatabase(configuration)
            .AddRepositories();

        return services;
    }

    private static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<NpgsqlDataSource>(sp =>
        {
            string? connectionString = configuration.GetConnectionString(Constants.DEFAULT_CONNECTION);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is missing or empty");
            }

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString)
            {
                Name = "auth-service-db",
            };

            dataSourceBuilder.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>());

            return dataSourceBuilder.Build();
        });

        services.AddDbContext<AuthServiceDbContext>((sp, options) =>
        {
            var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();

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

        services.AddScoped<ITransactionManager, TransactionManager>();
        services.AddScoped<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IAuthUserRepository, AuthUserRepository>();

        return services;
    }
}
