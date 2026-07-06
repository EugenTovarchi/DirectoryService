using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync($"/files/{Guid.NewGuid()}", null);

        // Assert
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
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("directory.read"));

        // Act
        HttpResponseMessage response = await client.PostAsync($"/files/{Guid.NewGuid()}", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMediaAssetInfo_WithFilesReadPermission_ShouldPassAuthorization()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("files.read"));

        // Act
        HttpResponseMessage response = await client.PostAsync($"/files/{Guid.NewGuid()}", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StartMultipartUpload_WithoutToken_ShouldReturnUnauthorized()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/files/multipart/start", new { });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartMultipartUpload_WithFilesReadPermission_ShouldReturnForbidden()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("files.read"));

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/files/multipart/start", new { });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StartMultipartUpload_WithFilesUploadPermission_ShouldPassAuthorization()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("files.upload"));

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/files/multipart/start", new { });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_WithFilesUploadPermission_ShouldReturnForbidden()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("files.upload"));

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/files/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_WithFilesDeletePermission_ShouldPassAuthorization()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenFactory.Create("files.delete"));

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/files/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
