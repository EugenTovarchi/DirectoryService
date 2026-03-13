using System.Net;
using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Core.HttpCommunication;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.IntegrationTests.Infrastructure;
using SharedService.SharedKernel;

namespace FileService.IntegrationTests.Features;

public class GetDownloadUrlTests : FileServiceBaseTests
{
    public GetDownloadUrlTests(FileServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetDownloadUrl_With_Valid_Data_Should_Succeed()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        await CreateTestBucketAsync(VideoAsset.LOCATION);

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken);

        Guid mediaAssetId = mediaAsset.Id;

        GetDownloadUrlRequest getDownloadUrlRequest = new(mediaAssetId);

        // Act
        HttpResponseMessage getResponse = await AppHttpClient
            .PostAsJsonAsync("/files/download/url", getDownloadUrlRequest, cancellationToken);

        Result<string, Failure> getResult = await getResponse
            .HandleResponseAsync<string>(cancellationToken);

        // Assert
        Assert.True(getResult.IsSuccess);
        Assert.NotNull(getResult.Value);
        string? downloadUrl = getResult.Value;
        Assert.StartsWith("http", downloadUrl);
        Assert.Contains(mediaAsset.Id.ToString(), downloadUrl);
    }

    [Fact]
    public async Task GetDownloadUrl_WithNonExistedAsset_ShouldReturnNotFound()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        var nonExistentId = Guid.NewGuid();
        var request = new GetDownloadUrlRequest(nonExistentId);

        // Act
        HttpResponseMessage response = await AppHttpClient
            .PostAsJsonAsync("/files/download/url", request, cancellationToken);

        Result<string, Failure> result = await response
            .HandleResponseAsync<string>(cancellationToken);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDownloadUrl_WithNonUploadedStatus_ShouldReturnNotFound()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.UPLOADING, cancellationToken);
        var request = new GetDownloadUrlRequest(mediaAsset.Id);

        // Act
        HttpResponseMessage response = await AppHttpClient
            .PostAsJsonAsync("/files/download/url", request, cancellationToken);

        Result<string, Failure> result = await response
            .HandleResponseAsync<string>(cancellationToken);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDownloadUrl_WithDeletedAsset_ShouldReturnNotFound()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.DELETED, cancellationToken);
        var request = new GetDownloadUrlRequest(mediaAsset.Id);

        // Act
        HttpResponseMessage response = await AppHttpClient
            .PostAsJsonAsync("/files/download/url", request, cancellationToken);

        Result<string, Failure> result = await response
            .HandleResponseAsync<string>(cancellationToken);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDownloadUrl_WithEmptyGuid_ShouldReturnBadRequest()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        var request = new GetDownloadUrlRequest(Guid.Empty);

        // Act
        HttpResponseMessage response = await AppHttpClient
            .PostAsJsonAsync("/files/download/url", request, cancellationToken);

        Result<string, Failure> result = await response
            .HandleResponseAsync<string>(cancellationToken);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}