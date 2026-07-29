using AuthService.Domain.Identity;
using AuthService.Infrastructure.Postgres;
using AuthService.Infrastructure.Postgres.Seeding;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthService.IntegrationTests.Features;

public sealed class AuthIdentitySeedTests : AuthServiceBaseTests
{
    public AuthIdentitySeedTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task SeedAsync_When_Called_Twice_Should_Not_Create_Duplicates()
    {
        // Arrange
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AuthIdentitySeeder seeder =
            scope.ServiceProvider.GetRequiredService<AuthIdentitySeeder>();

        // Act
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert
        var seedState = await ExecuteInDb(async dbContext => new
        {
            RoleCount = await dbContext.Roles.CountAsync(),
            PermissionCount = await dbContext.Permissions.CountAsync(),
            RolePermissionCount = await dbContext.RolePermissions.CountAsync(),
            ViewerPermissionCodes = await dbContext.RolePermissions
                .Where(rolePermission => rolePermission.Role.Name == AuthRoles.VIEWER)
                .Select(rolePermission => rolePermission.Permission.Code)
                .OrderBy(code => code)
                .ToListAsync()
        });

        seedState.RoleCount.Should().Be(5);
        seedState.PermissionCount.Should().Be(8);
        seedState.RolePermissionCount.Should().Be(29);
        seedState.ViewerPermissionCodes.Should().Equal(
            AuthPermissions.DIRECTORY_READ,
            AuthPermissions.FILES_READ,
            AuthPermissions.VIDEOS_READ);
    }

    [Fact]
    public async Task SeedAsync_When_Configured_User_Has_No_Roles_Should_Assign_Configured_Role()
    {
        // Arrange
        const string email = "local-viewer-repair@tests.local";
        const string existingPassword = "existing-viewer-password";
        const string configuredPassword = "different-configured-password";

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AuthIdentitySeeder defaultSeeder =
            scope.ServiceProvider.GetRequiredService<AuthIdentitySeeder>();
        await defaultSeeder.SeedAsync();

        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser(
            email,
            Username.Create(email).Value,
            DisplayName.Create("Viewer without role").Value,
            currentCompanyId: null);
        IdentityResult createResult = await userManager.CreateAsync(
            user,
            existingPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to create local viewer repair test user");
        }

        var usersOptions = new LocalUsersSeedOptions
        {
            Enabled = true,
            Password = configuredPassword,
            Users = new List<LocalUserSeedDefinition>
            {
                new LocalUserSeedDefinition
                {
                    Email = email,
                    DisplayName = "Configured Viewer",
                    Role = AuthRoles.VIEWER
                }
            }
        };
        AuthIdentitySeeder repairSeeder = CreateSeeder(
            scope.ServiceProvider,
            usersOptions);

        // Act
        await repairSeeder.SeedAsync();

        // Assert
        (await userManager.IsInRoleAsync(user, AuthRoles.VIEWER))
            .Should().BeTrue();
        (await userManager.CheckPasswordAsync(user, existingPassword))
            .Should().BeTrue();
        (await userManager.CheckPasswordAsync(user, configuredPassword))
            .Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_With_LocalUsers_Should_Create_One_User_For_Each_Configured_Role()
    {
        // Arrange
        const string password = "test-local-role-password";
        Guid companyId = Guid.Parse("5F4AE15C-77F9-4878-B9E7-D57E54C3B99A");

        var users = new List<LocalUserSeedDefinition>
        {
            CreateLocalUser(
                "system-admin@tests.local",
                AuthRoles.SYSTEM_ADMIN,
                null),
            CreateLocalUser(
                "company-admin@tests.local",
                AuthRoles.COMPANY_ADMIN,
                companyId),
            CreateLocalUser(
                "operator@tests.local",
                AuthRoles.OPERATOR,
                companyId),
            CreateLocalUser(
                "technician@tests.local",
                AuthRoles.TECHNICIAN,
                companyId),
            CreateLocalUser(
                "viewer@tests.local",
                AuthRoles.VIEWER,
                companyId)
        };

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AuthIdentitySeeder defaultSeeder =
            scope.ServiceProvider.GetRequiredService<AuthIdentitySeeder>();
        await defaultSeeder.SeedAsync();

        UserManager<ApplicationUser> setupUserManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existingViewer = new ApplicationUser(
            "viewer@tests.local",
            Username.Create("viewer@tests.local").Value,
            DisplayName.Create("Existing Viewer").Value,
            currentCompanyId: null);
        IdentityResult createViewerResult = await setupUserManager.CreateAsync(
            existingViewer,
            password);
        if (!createViewerResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to arrange existing local Viewer");
        }

        IdentityResult assignViewerRoleResult = await setupUserManager.AddToRoleAsync(
            existingViewer,
            AuthRoles.VIEWER);
        if (!assignViewerRoleResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to arrange existing local Viewer role");
        }

        AuthIdentitySeeder seeder = CreateSeeder(
            scope.ServiceProvider,
            new LocalUsersSeedOptions
            {
                Enabled = true,
                Password = password,
                Users = users
            });

        // Act
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert
        UserManager<ApplicationUser> userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (LocalUserSeedDefinition configuredUser in users)
        {
            ApplicationUser? user = await userManager.FindByEmailAsync(
                configuredUser.Email);
            user.Should().NotBeNull();

            IList<string> roles = await userManager.GetRolesAsync(user!);
            roles.Should().ContainSingle()
                .Which.Should().Be(configuredUser.Role);
            user.CurrentCompanyId.Should().Be(configuredUser.CurrentCompanyId);
        }
    }

    private static LocalUserSeedDefinition CreateLocalUser(
        string email,
        string role,
        Guid? currentCompanyId)
    {
        return new LocalUserSeedDefinition
        {
            Email = email,
            DisplayName = $"Local {role}",
            Role = role,
            CurrentCompanyId = currentCompanyId
        };
    }

    private static AuthIdentitySeeder CreateSeeder(
        IServiceProvider serviceProvider,
        LocalUsersSeedOptions usersOptions)
    {
        return new AuthIdentitySeeder(
            serviceProvider.GetRequiredService<AuthServiceDbContext>(),
            serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>(),
            serviceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            serviceProvider.GetRequiredService<IHostEnvironment>(),
            Options.Create(usersOptions),
            serviceProvider.GetRequiredService<ILogger<AuthIdentitySeeder>>());
    }
}
