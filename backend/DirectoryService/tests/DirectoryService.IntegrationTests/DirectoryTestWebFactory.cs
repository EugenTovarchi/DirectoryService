using System.Data.Common;
using DirectoryService.Application.Database;
using DirectoryService.Infrastructure.Postgres.BackgroundServices;
using DirectoryService.Infrastructure.Postgres.Database;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace DirectoryService.IntegrationTests;

public class DirectoryTestWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres")
        .WithDatabase("directory_service_tests")
        .WithUsername("postgresUser")
        .WithPassword("postgresPassword")
        .Build();

    private DbConnection _dbConnection = null!;
    private Respawner _respawner = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["ConnectionStrings:RabbitMq"] = "amqp://localhost:5672",
                ["Messaging:UseExternalTransports"] = "false",
                ["FileServiceOptions:Url"] = "http://localhost:9003/",
                ["FileServiceOptions:TimeoutSeconds"] = "10",
                ["OpenTelemetry:Enabled"] = "false"
            };

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDistributedCache>();
            services.RemoveAll<NpgsqlDataSource>();
            services.RemoveAll<INpgsqlConnectionFactory>();
            services.RemoveAll<NpgsqlConnectionFactory>();

            var cleanupServiceDescriptor = services.FirstOrDefault(d =>
                d.ImplementationType == typeof(DeleteExpireDepartmentsBackgroundService) ||
                string.Equals(
                    d.ImplementationType?.Name,
                    nameof(DeleteExpireDepartmentsBackgroundService),
                    StringComparison.Ordinal));

            if (cleanupServiceDescriptor != null)
            {
                services.Remove(cleanupServiceDescriptor);
            }

            services.AddSingleton(_ =>
            {
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(_dbContainer.GetConnectionString())
                {
                    Name = "directory-service-test-db"
                };

                return dataSourceBuilder.Build();
            });

            services.AddScoped<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
            services.AddScoped<NpgsqlConnectionFactory>();
            services.AddDistributedMemoryCache();
        });

        base.ConfigureWebHost(builder);
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        await CreateDatabaseDirectlyAsync();

        _dbConnection = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await _dbConnection.OpenAsync();

        await InitializeRespawnerAsync();
    }

    private async Task CreateDatabaseDirectlyAsync()
    {
        await using var dbContext = DirectoryServiceDbContext.Create(_dbContainer.GetConnectionString());
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        await dbContext.DisposeAsync();
    }

    private async Task InitializeRespawnerAsync()
    {
        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_dbConnection);

    public new async Task DisposeAsync()
    {
        if (_dbConnection != null)
        {
            await _dbConnection.CloseAsync();
            await _dbConnection.DisposeAsync();
        }

        await _dbContainer.StopAsync();
        await _dbContainer.DisposeAsync();
    }
}
