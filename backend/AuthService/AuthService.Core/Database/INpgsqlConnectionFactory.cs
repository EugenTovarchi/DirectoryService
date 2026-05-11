using System.Data;

namespace AuthService.Core.Database;

public interface INpgsqlConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
