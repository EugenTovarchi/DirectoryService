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
    private readonly LocalUsersSeedOptions _localUsersOptions;
    private readonly ILogger<AuthIdentitySeeder> _logger;

    public AuthIdentitySeeder(
        AuthServiceDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IHostEnvironment hostEnvironment,
        IOptions<LocalViewerSeedOptions> localViewerOptions,
        IOptions<LocalUsersSeedOptions> localUsersOptions,
        ILogger<AuthIdentitySeeder> logger)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _userManager = userManager;
        _hostEnvironment = hostEnvironment;
        _localViewerOptions = localViewerOptions.Value;
        _localUsersOptions = localUsersOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Создаёт отсутствующие роли, permissions, связи и явно включённых local users.
    /// Повторный вызов не создаёт дубликаты и не сбрасывает пароли существующих users.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolePermissionsAsync(cancellationToken);
        await SeedConfiguredLocalUsersAsync();

        _logger.LogInformation("Auth identity seed completed");
    }

    private async Task SeedConfiguredLocalUsersAsync()
    {
        var configuredUsers = new List<LocalUserSeedDefinition>();

        if (_localViewerOptions.Enabled)
        {
            configuredUsers.Add(new LocalUserSeedDefinition
            {
                Email = _localViewerOptions.Email,
                Password = _localViewerOptions.Password,
                DisplayName = _localViewerOptions.DisplayName,
                Role = AuthRoles.VIEWER
            });
        }

        if (_localUsersOptions.Enabled)
        {
            configuredUsers.AddRange(_localUsersOptions.Users);
        }

        if (configuredUsers.Count == 0)
        {
            return;
        }

        if (!_hostEnvironment.IsDevelopment() &&
            !_hostEnvironment.IsEnvironment("Docker") &&
            !_hostEnvironment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "Local user seed is allowed only in Development, Docker or Testing environment");
        }

        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (LocalUserSeedDefinition configuredUser in configuredUsers)
        {
            string email = configuredUser.Email.Trim();
            if (!emails.Add(email))
            {
                throw new InvalidOperationException(
                    "Local user seed contains duplicate configured email");
            }

            string password = string.IsNullOrWhiteSpace(configuredUser.Password)
                ? _localUsersOptions.Password
                : configuredUser.Password;

            await SeedLocalUserAsync(configuredUser, email, password);
        }
    }

    private async Task SeedLocalUserAsync(
        LocalUserSeedDefinition configuredUser,
        string email,
        string password)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(configuredUser.DisplayName) ||
            string.IsNullOrWhiteSpace(configuredUser.Role))
        {
            throw new InvalidOperationException(
                "Local user seed requires Email, Password, DisplayName and Role");
        }

        ApplicationUser? existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            IList<string> existingRoles = await _userManager.GetRolesAsync(existingUser);

            if (existingRoles.Contains(configuredUser.Role, StringComparer.Ordinal))
            {
                await RepairMissingCompanyContextAsync(
                    existingUser,
                    configuredUser.CurrentCompanyId);

                _logger.LogInformation(
                    "Local user already exists with role {Role}",
                    configuredUser.Role);
                return;
            }

            if (existingRoles.Count > 0)
            {
                throw new InvalidOperationException(
                    "Configured local user already exists with another role");
            }

            IdentityResult repairRoleResult = await _userManager.AddToRoleAsync(
                existingUser,
                configuredUser.Role);
            EnsureSucceeded(repairRoleResult, "repair local user role");

            _logger.LogInformation(
                "Assigned missing role {Role} to existing local user",
                configuredUser.Role);
            return;
        }

        var usernameResult = Username.Create(email);
        if (usernameResult.IsFailure)
        {
            throw new InvalidOperationException(
                "Local user seed Email cannot be used as username");
        }

        var displayNameResult = DisplayName.Create(configuredUser.DisplayName);
        if (displayNameResult.IsFailure)
        {
            throw new InvalidOperationException(
                "Local user seed DisplayName is invalid");
        }

        ApplicationUser user = new(
            email,
            usernameResult.Value,
            displayNameResult.Value,
            configuredUser.CurrentCompanyId);
        IdentityResult createResult = await _userManager.CreateAsync(
            user,
            password);
        EnsureSucceeded(createResult, "create local user");

        IdentityResult addToRoleResult = await _userManager.AddToRoleAsync(
            user,
            configuredUser.Role);
        EnsureSucceeded(addToRoleResult, "assign local user role");

        _logger.LogInformation(
            "Local user created with role {Role}",
            configuredUser.Role);
    }

    private async Task RepairMissingCompanyContextAsync(
        ApplicationUser user,
        Guid? configuredCompanyId)
    {
        if (user.CurrentCompanyId is not null ||
            configuredCompanyId is not Guid companyId)
        {
            return;
        }

        user.ChangeCurrentCompany(companyId);
        IdentityResult updateResult = await _userManager.UpdateAsync(user);
        EnsureSucceeded(updateResult, "repair local user company context");

        _logger.LogInformation(
            "Assigned missing company context to existing local user");
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
