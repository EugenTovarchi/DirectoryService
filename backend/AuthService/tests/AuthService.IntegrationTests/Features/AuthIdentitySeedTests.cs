using AuthService.Domain.Identity;
using AuthService.Infrastructure.Postgres.Seeding;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        await using var scope = Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<AuthIdentitySeeder>();

        await seeder.SeedAsync();
        await seeder.SeedAsync();

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
        seedState.PermissionCount.Should().Be(7);
        seedState.RolePermissionCount.Should().Be(26);
        seedState.ViewerPermissionCodes.Should().Equal(
            AuthPermissions.DIRECTORY_READ,
            AuthPermissions.FILES_READ,
            AuthPermissions.VIDEOS_READ);
    }
}
