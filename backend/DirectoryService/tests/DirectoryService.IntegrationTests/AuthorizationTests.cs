using System.Net;
using System.Net.Http.Headers;

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
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/departments/roots?page=1&pageSize=20");

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
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("files.read"));

        HttpResponseMessage response = await client.GetAsync("/api/departments/roots?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDepartmentRoots_WithDirectoryReadPermission_ShouldSucceed()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("directory.read"));

        HttpResponseMessage response = await client.GetAsync("/api/departments/roots?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
