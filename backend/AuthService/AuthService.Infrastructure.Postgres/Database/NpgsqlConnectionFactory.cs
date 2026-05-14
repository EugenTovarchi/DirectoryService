using System.Data;
using AuthService.Core.Database;
using Npgsql;

namespace AuthService.Infrastructure.Postgres.Database;

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
