using System.Net;
using System.Net.Http.Headers;
using FileService.IntegrationTests.Infrastructure;

namespace FileService.IntegrationTests.Features;

public class FileAuthorizationTests : FileServiceBaseTests
{
    private readonly FileServiceTestWebFactory _factory;

    public FileAuthorizationTests(FileServiceTestWebFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMediaAssetInfo_WithoutToken_ShouldReturnUnauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync($"/files/{Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMediaAssetInfo_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "invalid-token");

        // Act
        HttpResponseMessage response = await client.PostAsync($"/files/{Guid.NewGuid()}", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMediaAssetInfo_WithExpiredToken_ShouldReturnUnauthorized()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.CreateExpired("files.read"));

        // Act
        HttpResponseMessage response = await client.PostAsync($"/files/{Guid.NewGuid()}", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMediaAssetInfo_WithoutRequiredPermission_ShouldReturnForbidden()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("directory.read"));

        HttpResponseMessage response = await client.PostAsync($"/files/{Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMediaAssetInfo_WithFilesReadPermission_ShouldPassAuthorization()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("files.read"));

        HttpResponseMessage response = await client.PostAsync($"/files/{Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
