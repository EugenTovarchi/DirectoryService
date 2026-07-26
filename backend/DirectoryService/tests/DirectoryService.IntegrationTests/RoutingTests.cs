using System.Net;

namespace DirectoryService.IntegrationTests;

public class RoutingTests : DirectoryBaseTests
{
    private readonly DirectoryTestWebFactory _factory;

    public RoutingTests(DirectoryTestWebFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LocationsRoute_WithoutToken_ShouldReachProtectedApiRoute()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/locations");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LegacyControllerPrefixedLocationsRoute_ShouldNotExist()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/Location/api/locations");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
