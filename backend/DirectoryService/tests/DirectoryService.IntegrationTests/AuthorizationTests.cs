using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DirectoryService.Contracts.Requests.Locations;

namespace DirectoryService.IntegrationTests;

public class AuthorizationTests : DirectoryBaseTests
{
    private readonly DirectoryTestWebFactory _factory;

    public AuthorizationTests(DirectoryTestWebFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDepartmentRoots_WithoutToken_ShouldReturnUnauthorized()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/departments/roots?page=1&pageSize=20");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDepartmentRoots_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "invalid-token");

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/departments/roots?page=1&pageSize=20");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDepartmentRoots_WithExpiredToken_ShouldReturnUnauthorized()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.CreateExpired("directory.read"));

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/departments/roots?page=1&pageSize=20");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDepartmentRoots_WithoutRequiredPermission_ShouldReturnForbidden()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("files.read"));

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/departments/roots?page=1&pageSize=20");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDepartmentRoots_WithDirectoryReadPermission_ShouldSucceed()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("directory.read"));

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/departments/roots?page=1&pageSize=20");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateLocation_WithoutToken_ShouldReturnUnauthorized()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        CreateLocationRequest request = CreateValidLocationRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/locations", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateLocation_WithDirectoryReadPermission_ShouldReturnForbidden()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("directory.read"));
        CreateLocationRequest request = CreateValidLocationRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/locations", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateLocation_WithDirectoryManagePermission_ShouldSucceed()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("directory.manage"));
        CreateLocationRequest request = CreateValidLocationRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/locations", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static CreateLocationRequest CreateValidLocationRequest() => new(
        "Authorization test location",
        "Europe/Moscow",
        new LocationAddressRequest("Russia", "Moscow", "Tverskaya", "1"));
}
