using System.Data;

namespace DirectoryService.Application.Database;

public interface INpgsqlConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);

}
