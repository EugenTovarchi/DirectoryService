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

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken);

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
        Assert.NotNull(assetInfo.Url);
        Assert.StartsWith("http", assetInfo.Url);
        Assert.Contains(mediaAsset.Id.ToString(), assetInfo.Url);
    }

    [Fact]
    public async Task GetMediaAssetInfo_With_Uploading_Status_ShouldReturnNull()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.UPLOADING, cancellationToken);

        // Act
        string url = $"/files/{mediaAsset.Id}";

        HttpResponseMessage getResponse = await AppHttpClient
            .PostAsync(url, null, cancellationToken);

        Result<GetMediaAssetResponse, Failure> getResult = (await getResponse
            .HandleNullableResponseAsync<GetMediaAssetResponse>(cancellationToken))!;

        // Assert
        Assert.True(getResult.IsSuccess);
        Assert.Null(getResult.Value);
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
    public async Task GetMediaAssetInfo_With_Valid_But_NonExistent_Guid_Should_Return_Null()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await AppHttpClient.PostAsync($"/files/{nonExistentId}", null);

        var result = await response.HandleNullableResponseAsync<GetMediaAssetResponse>();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}