using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AuthService.IntegrationTests.Infrastructure;

public sealed class AuthServiceDbContextModelTests : AuthServiceBaseTests
{
    public AuthServiceDbContextModelTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task EfModel_ShouldNotContainLegacyRefreshTokenPersistence()
    {
        // Arrange
        const string legacyEntityName = "AuthService.Domain.Identity.RefreshToken";
        const string legacyTableName = "refresh_tokens";

        // Act
        List<IEntityType> entityTypes = await ExecuteInDb(dbContext =>
            Task.FromResult(dbContext.Model.GetEntityTypes().ToList()));

        // Assert
        entityTypes.Should().NotContain(entityType =>
            entityType.ClrType.FullName == legacyEntityName);
        entityTypes.Select(entityType => entityType.GetTableName())
            .Should().NotContain(legacyTableName);
    }
}
