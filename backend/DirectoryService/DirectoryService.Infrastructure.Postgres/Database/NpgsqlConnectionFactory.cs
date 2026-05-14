using System.Data;
using DirectoryService.Application.Database;
using Npgsql;

namespace DirectoryService.Infrastructure.Postgres.Database;

/// <summary>
/// Фабрика открывает соединение с БД.
/// </summary>
public class NpgsqlConnectionFactory : INpgsqlConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await _dataSource.OpenConnectionAsync(cancellationToken);
    }
}
