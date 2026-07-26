using AuthService.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Postgres.Seeding;

/// <summary>
/// Идемпотентно создает стартовые roles, permissions и связи role-permission.
/// </summary>
public sealed class AuthIdentitySeeder
{
    private readonly AuthServiceDbContext _dbContext;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly LocalViewerSeedOptions _localViewerOptions;
    private readonly ILogger<AuthIdentitySeeder> _logger;

    public AuthIdentitySeeder(
        AuthServiceDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IHostEnvironment hostEnvironment,
        IOptions<LocalViewerSeedOptions> localViewerOptions,
        ILogger<AuthIdentitySeeder> logger)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _userManager = userManager;
        _hostEnvironment = hostEnvironment;
        _localViewerOptions = localViewerOptions.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolePermissionsAsync(cancellationToken);
        await SeedLocalViewerAsync();

        _logger.LogInformation("Auth identity seed completed");
    }

    private async Task SeedLocalViewerAsync()
    {
        if (!_localViewerOptions.Enabled)
            return;

        if (!_hostEnvironment.IsDevelopment() && !_hostEnvironment.IsEnvironment("Docker"))
            throw new InvalidOperationException("Local viewer seed is allowed only in Development or Docker environment");

        string email = _localViewerOptions.Email.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(_localViewerOptions.Password))
            throw new InvalidOperationException("Local viewer seed requires Email and Password configuration");

        ApplicationUser? existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            bool isViewer = await _userManager.IsInRoleAsync(existingUser, AuthRoles.VIEWER);
            if (!isViewer)
                throw new InvalidOperationException("Configured local viewer already exists without Viewer role");

            _logger.LogInformation("Local Viewer user already exists");
            return;
        }

        var usernameResult = Username.Create(email);
        if (usernameResult.IsFailure)
            throw new InvalidOperationException("Local viewer seed Email cannot be used as username");

        var displayNameResult = DisplayName.Create(_localViewerOptions.DisplayName);
        if (displayNameResult.IsFailure)
            throw new InvalidOperationException("Local viewer seed DisplayName is invalid");

        ApplicationUser user = new(email, usernameResult.Value, displayNameResult.Value, currentCompanyId: null);
        IdentityResult createResult = await _userManager.CreateAsync(user, _localViewerOptions.Password);
        EnsureSucceeded(createResult, "create local Viewer user");

        IdentityResult addToRoleResult = await _userManager.AddToRoleAsync(user, AuthRoles.VIEWER);
        EnsureSucceeded(addToRoleResult, "assign Viewer role to development user");

        _logger.LogInformation("Local Viewer user created");
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
            return;

        string errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Failed to {operation}: {errors}");
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
