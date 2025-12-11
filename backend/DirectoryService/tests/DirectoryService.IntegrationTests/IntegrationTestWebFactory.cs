using DirectoryService.Application.Database;
using DirectoryService.Core.Abstractions;
using DirectoryService.Infrastructure.Postgres.Database;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.Infrastructure.Postgres.Repositories;
using DirectoryService.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Respawn;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using Testcontainers.PostgreSql;

namespace DirectoryService.IntegrationTests;

public class IntegrationTestWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres")
        .WithDatabase("pet_family_tests")
        .WithUsername("postgresUser")  
        .WithPassword("postgresPassword") 
        .Build();

    private DbConnection _dbConnection = default!;
    private Respawner _respawner = default!;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();
        CreateTestDatabaseTables(host);
        host.Start();
        return host;
    }

    private void CreateTestDatabaseTables(IHost host)
    {
        using var scope = host.Services.CreateScope();

        var speciesDbContext = scope.ServiceProvider.GetRequiredService<DirectoryServiceDbContext>();
        speciesDbContext.Database.EnsureCreated();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            //удаляю обработчики и хендлеры
            RemoveAllHandlersAndServices(services);

            var testConnectionString = _dbContainer.GetConnectionString();

            services.AddScoped(_ => new DirectoryServiceDbContext(testConnectionString));
            services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            services.AddScoped<IPositionRepository, PositionRepository>();
            services.AddScoped<ITransactionScope, TransactionScope>();
            services.AddScoped<ILocationRepository, LocationRepository>();
            //services.AddScoped<IReadSpeciesDbContext>(provider =>
            //    provider.GetRequiredService<ReadSpeciesDbContext>());
            services.AddSingleton<INpgsqlConnectionFactory>(_ => new NpgsqlConnectionFactory(testConnectionString));
        });
    }


    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DirectoryServiceDbContext>();
        //Создаём миграции(schemas)
        await dbContext.Database.EnsureCreatedAsync();

        _dbConnection = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await InitilizeRespawner();
    }

    private async Task InitilizeRespawner()
    {
        await _dbConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"] //таблица с таким название создается при тесте
        });

    }

    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_dbConnection);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await _dbContainer.DisposeAsync();
    }

    private void RemoveAllHandlersAndServices(IServiceCollection services)
    {
        var commandHandlers = services
            .Where(s => s.ServiceType.IsGenericType &&
                       s.ServiceType.GetGenericTypeDefinition() == typeof(ICommandHandler<,>))
            .ToList();

        var queryHandlers = services
            .Where(s => s.ServiceType.IsGenericType &&
                       s.ServiceType.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))
            .ToList();

        // Удаляем все конкретные обработчики
        var handlers = services
            .Where(s => s.ServiceType.Namespace?.Contains("Application.Commands") == true ||
                       s.ServiceType.Namespace?.Contains("Application.Queries") == true ||
                       s.ImplementationType?.Name.EndsWith("Handler") == true)
            .ToList();

        foreach (var descriptor in commandHandlers.Concat(queryHandlers).Concat(handlers))
        {
            services.Remove(descriptor);
        }

        var dbContexts = services
        .Where(s => s.ServiceType.Name.Contains("DbContext") ||
                   s.ImplementationType?.Name.Contains("DbContext") == true)
        .ToList();

        foreach (var descriptor in dbContexts)
        {
            services.Remove(descriptor);
        }

        var sqlFactories = services
            .Where(s => s.ServiceType == typeof(INpgsqlConnectionFactory) ||
                       s.ImplementationType == typeof(SqlConnectionFactory))
            .ToList();

        foreach (var descriptor in sqlFactories)
        {
            services.Remove(descriptor);
        }
    }
}
