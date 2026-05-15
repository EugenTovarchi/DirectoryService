using System.Data.Common;
using AuthService.Core.Database;
using AuthService.Infrastructure.Postgres;
using AuthService.Infrastructure.Postgres.Database;
using AuthService.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace AuthService.IntegrationTests.Infrastructure;

public class AuthServiceTestWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("auth-service")
        .WithUsername("postgresUser")
        .WithPassword("postgresPassword")
        .Build();

    private DbConnection _dbConnection = null!;
    private Respawner _respawner = null!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await CreateDatabaseDirectlyAsync();

        _dbConnection = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await _dbConnection.OpenAsync();

        await InitializeRespawnerAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                ["Jwt:Issuer"] = "24eye.auth.tests",
                ["Jwt:Audience"] = "24eye.backend.tests",
                ["Jwt:SigningKey"] = "test-auth-service-signing-key-with-enough-length",
                ["Jwt:AccessTokenLifetimeMinutes"] = "15",
                ["Jwt:RefreshTokenLifetimeDays"] = "30"
            };

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AuthServiceDbContext>();
            services.RemoveAll<NpgsqlDataSource>();
            services.RemoveAll<INpgsqlConnectionFactory>();

            services.AddDbContext<AuthServiceDbContext>(_ =>
                AuthServiceDbContext.Create(_dbContainer.GetConnectionString()));

            services.AddSingleton(sp =>
            {
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(_dbContainer.GetConnectionString())
                {
                    Name = "auth-service-db"
                };

                dataSourceBuilder.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>());

                return dataSourceBuilder.Build();
            });

            services.AddScoped<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
        });

        base.ConfigureWebHost(builder);
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

    private async Task CreateDatabaseDirectlyAsync()
    {
        await using var dbContext = AuthServiceDbContext.Create(_dbContainer.GetConnectionString());

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    private async Task InitializeRespawnerAsync()
    {
        _respawner = await Respawner.CreateAsync(
            _dbConnection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = ["__EFMigrationsHistory"]
            });
    }
}
