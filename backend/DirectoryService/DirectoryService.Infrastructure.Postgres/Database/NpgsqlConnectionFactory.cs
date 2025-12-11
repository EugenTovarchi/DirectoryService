using DirectoryService.Application.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

namespace DirectoryService.Infrastructure.Postgres.Database;

/// <summary>
/// Фабрика открывает соединение с БД.
/// </summary>
public  class NpgsqlConnectionFactory :IDisposable, IAsyncDisposable, INpgsqlConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(IConfiguration configuration)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(configuration.GetConnectionString(Constants.DEFAULT_CONNECTION));
        dataSourceBuilder.UseLoggerFactory(CreateLoggerFactory());

        _dataSource = dataSourceBuilder.Build();
    } 

    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await _dataSource.OpenConnectionAsync();
    }

    public void Dispose()
    {
        _dataSource.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();   
    }

    private ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(builder => { builder.AddConsole(); });

}
