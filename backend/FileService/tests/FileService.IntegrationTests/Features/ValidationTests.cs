using System.Net;
using System.Net.Http.Json;
using FileService.Contracts.Requests;
using FileService.IntegrationTests.Infrastructure;

namespace FileService.IntegrationTests.Features;

public class ValidationTests : FileServiceBaseTests
{
    public ValidationTests(FileServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task StartMultipartUpload_With_Empty_OwnerId_Should_Return_BadRequest()
    {
        // Arrange
        await CreateTestBucketAsync("file-service-videos");

        var request = new StartMultipartUploadRequest(
            "test.mp4",
            "video",
            "video/mp4",
            1024,
            TEST_OWNER_TYPE,
            Guid.Empty);

        // Act
        var response = await AppHttpClient.PostAsJsonAsync("/files/multipart/start", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StartMultipartUpload_With_Invalid_OwnerType_Should_Return_BadRequest()
    {
        // Arrange
        await CreateTestBucketAsync("file-service-videos");

        var request = new StartMultipartUploadRequest(
            "test.mp4",
            "video",
            "video/mp4",
            1024,
            "invalid_type",
            TEST_DEPARTMENT_ID);

        // Act
        var response = await AppHttpClient.PostAsJsonAsync("/files/multipart/start", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StartMultipartUpload_With_Invalid_AssetType_Should_Return_BadRequest()
    {
        // Arrange
        await CreateTestBucketAsync("file-service-videos");

        var request = new StartMultipartUploadRequest(
            "test.mp4",
            "invalid_asset",
            "video/mp4",
            1024,
            TEST_OWNER_TYPE,
            TEST_DEPARTMENT_ID);

        // Act
        var response = await AppHttpClient.PostAsJsonAsync("/files/multipart/start", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StartMultipartUpload_With_Valid_Data_Should_Succeed()
    {
        // Arrange
        await CreateTestBucketAsync("file-service-videos");

        var request = new StartMultipartUploadRequest(
            "test.mp4",
            "video",
            "video/mp4",
            1024,
            TEST_OWNER_TYPE,
            TEST_DEPARTMENT_ID);

        // Act
        var response = await AppHttpClient.PostAsJsonAsync("/files/multipart/start", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}