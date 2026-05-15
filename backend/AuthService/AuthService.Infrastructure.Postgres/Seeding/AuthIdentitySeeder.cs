using AuthService.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthService.Infrastructure.Postgres.Seeding;

/// <summary>
/// Идемпотентно создает стартовые roles, permissions и связи role-permission.
/// </summary>
public sealed class AuthIdentitySeeder
{
    private readonly AuthServiceDbContext _dbContext;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<AuthIdentitySeeder> _logger;

    public AuthIdentitySeeder(
        AuthServiceDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        ILogger<AuthIdentitySeeder> logger)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolePermissionsAsync(cancellationToken);

        _logger.LogInformation("Auth identity seed completed");
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in AuthIdentitySeedData.Roles)
        {
            if (await _roleManager.RoleExistsAsync(role.Name))
                continue;

            var result = await _roleManager.CreateAsync(new ApplicationRole(role.Name, role.Description));

            if (!result.Succeeded)
            {
                string errors = string.Join("; ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to seed auth role '{role.Name}': {errors}");
            }
        }
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await _dbContext.Permissions
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);

        var existingCodeSet = existingCodes.ToHashSet(StringComparer.Ordinal);

        foreach (var permission in AuthIdentitySeedData.Permissions)
        {
            if (existingCodeSet.Contains(permission.Code))
                continue;

            _dbContext.Permissions.Add(new Permission(permission.Code, permission.Description));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .ToDictionaryAsync(role => role.Name!, StringComparer.Ordinal, cancellationToken);

        var permissions = await _dbContext.Permissions
            .ToDictionaryAsync(permission => permission.Code, StringComparer.Ordinal, cancellationToken);

        var existingLinks = await _dbContext.RolePermissions
            .Select(rolePermission => new
            {
                rolePermission.RoleId,
                rolePermission.PermissionId
            })
            .ToListAsync(cancellationToken);

        var existingLinkSet = existingLinks
            .Select(link => (link.RoleId, link.PermissionId))
            .ToHashSet();

        foreach (var (roleName, permissionCodes) in AuthIdentitySeedData.RolePermissions)
        {
            var role = roles[roleName];

            foreach (string permissionCode in permissionCodes)
            {
                var permission = permissions[permissionCode];
                var linkKey = (role.Id, permission.Id);

                if (existingLinkSet.Contains(linkKey))
                    continue;

                _dbContext.RolePermissions.Add(new RolePermission(role.Id, permission.Id));
                existingLinkSet.Add(linkKey);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
