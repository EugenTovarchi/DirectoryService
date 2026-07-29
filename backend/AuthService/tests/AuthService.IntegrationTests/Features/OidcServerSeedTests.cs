using AuthService.Infrastructure.Postgres.Seeding;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace AuthService.IntegrationTests.Features;

public sealed class OidcServerSeedTests : AuthServiceBaseTests
{
    public OidcServerSeedTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task SeedAsync_When_Called_Twice_Should_Create_Unique_Scopes_And_Clients()
    {
        // Arrange
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<OidcServerSeeder>();

        // Act
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert
        var seedState = await ExecuteInDb(async dbContext => new
        {
            Applications = await dbContext
                .Set<OpenIddictEntityFrameworkCoreApplication>()
                .OrderBy(application => application.ClientId)
                .Select(application => new
                {
                    application.ClientId,
                    application.ClientType,
                    application.ClientSecret
                })
                .ToListAsync(),
            Scopes = await dbContext
                .Set<OpenIddictEntityFrameworkCoreScope>()
                .OrderBy(oidcScope => oidcScope.Name)
                .Select(oidcScope => oidcScope.Name)
                .ToListAsync()
        });

        seedState.Applications.Should().HaveCount(3);
        seedState.Scopes.Should().Equal("auth", "directory", "files");

        var publicClient = seedState.Applications.Single(
            application => application.ClientId == "oidc-public-tests");
        publicClient.ClientType.Should().Be(OpenIddictConstants.ClientTypes.Public);
        publicClient.ClientSecret.Should().BeNull();

        var confidentialClient = seedState.Applications.Single(
            application => application.ClientId == "oidc-confidential-tests");
        confidentialClient.ClientType.Should().Be(OpenIddictConstants.ClientTypes.Confidential);
        confidentialClient.ClientSecret.Should().NotBeNullOrWhiteSpace();
        confidentialClient.ClientSecret.Should()
            .NotBe("test-oidc-confidential-client-secret-value");

        var serviceClient = seedState.Applications.Single(
            application => application.ClientId == "directory-service");
        serviceClient.ClientType.Should().Be(OpenIddictConstants.ClientTypes.Confidential);
        serviceClient.ClientSecret.Should().NotBeNullOrWhiteSpace();
        serviceClient.ClientSecret.Should()
            .NotBe("test-directory-service-client-secret-value");
    }
}
