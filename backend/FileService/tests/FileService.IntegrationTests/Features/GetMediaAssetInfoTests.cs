using System.Net;
using CSharpFunctionalExtensions;
using FileService.Contracts.Responses;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.IntegrationTests.Infrastructure;
using SharedService.Framework.ControllersResults;
using SharedService.SharedKernel;

namespace FileService.IntegrationTests.Features;

public class GetMediaAssetInfoTests : FileServiceBaseTests
{
    public GetMediaAssetInfoTests(FileServiceTestWebFactory factory)
        : base(factory) { }

    [Fact]
    public async Task GetMediaAssetInfo_Valid_Data_Should_Succeed()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        FileInfo file = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));

        await CreateTestBucketAsync(VideoAsset.LOCATION);

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken: cancellationToken);

        Guid mediaAssetId = mediaAsset.Id;

        // Act
        string url = $"/files/{mediaAssetId}";

        HttpResponseMessage getResponse = await AppHttpClient
            .PostAsync(url, null, cancellationToken);

        Result<GetMediaAssetResponse, Failure> getResult = await getResponse
            .HandleResponseAsync<GetMediaAssetResponse>(cancellationToken);

        // Assert
        Assert.True(getResult.IsSuccess);
        Assert.NotNull(getResult.Value);
        var assetInfo = getResult.Value;
        Assert.Equal(mediaAsset.Id, assetInfo.Id);
        Assert.Equal("uploaded", assetInfo.Status);
        Assert.Equal("video", assetInfo.AssetType);
        Assert.Equal("test-file.mp4", assetInfo.FileName);
        Assert.Equal("video/mp4", assetInfo.ContentType);
        Assert.Equal(file.Length, assetInfo.Size);
        Assert.Null(assetInfo.ViewUrl);
        Assert.NotNull(assetInfo.DownloadUrl);
        Assert.Null(assetInfo.ThumbnailUrl);
        Assert.StartsWith("http", assetInfo.DownloadUrl, StringComparison.Ordinal);
        Assert.Contains(mediaAsset.Id.ToString(), assetInfo.DownloadUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMediaAssetInfo_With_Ready_Video_Should_Return_View_And_Download_Urls()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.READY, cancellationToken: cancellationToken);

        // Act
        HttpResponseMessage getResponse = await AppHttpClient
            .PostAsync($"/files/{mediaAsset.Id}", null, cancellationToken);

        Result<GetMediaAssetResponse, Failure> getResult = await getResponse
            .HandleResponseAsync<GetMediaAssetResponse>(cancellationToken);

        // Assert
        Assert.True(getResult.IsSuccess);
        Assert.NotNull(getResult.Value);
        Assert.Equal("ready", getResult.Value.Status);
        Assert.NotNull(getResult.Value.ViewUrl);
        Assert.NotNull(getResult.Value.DownloadUrl);
        Assert.Contains("master.m3u8", getResult.Value.ViewUrl, StringComparison.Ordinal);
        Assert.Contains(mediaAsset.Id.ToString(), getResult.Value.DownloadUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMediaAssetInfo_With_Uploading_Status_ShouldReturnNotFound()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.UPLOADING, cancellationToken: cancellationToken);

        // Act
        string url = $"/files/{mediaAsset.Id}";

        HttpResponseMessage getResponse = await AppHttpClient
            .PostAsync(url, null, cancellationToken);

        Result<GetMediaAssetResponse, Failure> getResult = await getResponse
            .HandleResponseAsync<GetMediaAssetResponse>(cancellationToken);

        // Assert
        Assert.True(getResult.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetMediaAssetInfo_With_Invalid_Id_Should_Return_Error()
    {
        // Act
        var response = await AppHttpClient.PostAsync("/files/invalid-id", null);

        var result = await response.HandleResponseAsync<GetMediaAssetResponse>();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMediaAssetInfo_With_Valid_But_NonExistent_Guid_Should_Return_NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AppHttpClient.PostAsync($"/files/{nonExistentId}", null);

        var result = await response.HandleResponseAsync<GetMediaAssetResponse>();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMediaAssetInfo_With_Empty_Guid_Should_Return_BadRequest()
    {
        // Act
        var response = await AppHttpClient.PostAsync($"/files/{Guid.Empty}", null);

        var result = await response.HandleResponseAsync<GetMediaAssetResponse>();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
