using AuthService.Core.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Postgres.Queries;

public sealed class RolePermissionReader : IRolePermissionReader
{
    private readonly AuthServiceDbContext _dbContext;

    public RolePermissionReader(AuthServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionCodesAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken)
    {
        if (roleNames.Count == 0)
            return [];

        return await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => roleNames.Contains(rolePermission.Role.Name!))
            .Select(rolePermission => rolePermission.Permission.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);
    }
}
