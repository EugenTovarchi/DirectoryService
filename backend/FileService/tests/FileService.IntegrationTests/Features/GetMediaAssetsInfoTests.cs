using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Domain;
using FileService.IntegrationTests.Infrastructure;
using SharedService.Framework.ControllersResults;
using SharedService.SharedKernel;

namespace FileService.IntegrationTests.Features;

public class GetMediaAssetsInfoTests : FileServiceBaseTests
{
    public GetMediaAssetsInfoTests(FileServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetMediaAssetsInfo_Valid_Data_Should_Succeed()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var mediaAsset1 = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken: cancellationToken);
        var mediaAsset2 = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken: cancellationToken);

        Guid mediaAssetId1 = mediaAsset1.Id;
        Guid mediaAssetId2 = mediaAsset2.Id;

        List<Guid> mediaAssetIds = [mediaAssetId1, mediaAssetId2];

        var getMediaAssetsInfoRequest = new GetMediaAssetsRequest(mediaAssetIds);

        // Act
        HttpResponseMessage getResponse = await AppHttpClient
            .PostAsJsonAsync("/files/batch", getMediaAssetsInfoRequest, cancellationToken);

        Result<GetMediaAssetsResponse, Failure> getResult = (await getResponse
            .HandleResponseAsync<GetMediaAssetsResponse>(cancellationToken))!;

        // Assert
        Assert.True(getResult.IsSuccess);
        Assert.NotNull(getResult.Value);

        var response = getResult.Value;
        Assert.NotNull(response.MediaAssets);
        Assert.Equal(2, response.MediaAssets.Count);

        var asset1Info = response.MediaAssets.FirstOrDefault(a => a.Id == mediaAsset1.Id);

        Assert.NotNull(asset1Info);
        Assert.Equal(mediaAsset1.Id, asset1Info.Id);
        Assert.Null(asset1Info.ViewUrl);
        Assert.NotNull(asset1Info.DownloadUrl);

        var asset2Info = response.MediaAssets.FirstOrDefault(a => a.Id == mediaAsset2.Id);

        Assert.NotNull(asset2Info);
        Assert.Equal(asset2Info.Id, asset2Info.Id);
        Assert.Null(asset2Info.ViewUrl);
        Assert.NotNull(asset2Info.DownloadUrl);

        Assert.NotEqual(asset1Info.DownloadUrl, asset2Info.DownloadUrl);
    }

    [Fact]
    public async Task GetMediaAssetsInfo_WithReadyVideo_ShouldReturnViewUrl()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.READY, cancellationToken: cancellationToken);
        var request = new GetMediaAssetsRequest([mediaAsset.Id]);

        // Act
        HttpResponseMessage getResponse = await AppHttpClient
            .PostAsJsonAsync("/files/batch", request, cancellationToken);

        Result<GetMediaAssetsResponse, Failure> getResult = (await getResponse
            .HandleResponseAsync<GetMediaAssetsResponse>(cancellationToken))!;

        // Assert
        Assert.True(getResult.IsSuccess);
        Assert.Single(getResult.Value.MediaAssets);
        var assetInfo = getResult.Value.MediaAssets[0];
        Assert.Equal("ready", assetInfo.Status);
        Assert.NotNull(assetInfo.ViewUrl);
        Assert.NotNull(assetInfo.DownloadUrl);
        Assert.Contains("master.m3u8", assetInfo.ViewUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMediaAssetsInfo_WithMixedExistingAndNonExisted_ShouldReturnOnlyExisting()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var existingAsset1 = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken: cancellationToken);
        var existingAsset2 = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken: cancellationToken);
        var nonExistentId = Guid.NewGuid();

        List<Guid> mixedIds = [existingAsset1.Id, nonExistentId, existingAsset2.Id];
        var getMediaAssetsInfoRequest = new GetMediaAssetsRequest(mixedIds);

        // Act
        HttpResponseMessage getResponse = await AppHttpClient
            .PostAsJsonAsync("/files/batch", getMediaAssetsInfoRequest, cancellationToken);

        Result<GetMediaAssetsResponse, Failure> getResult = (await getResponse
            .HandleResponseAsync<GetMediaAssetsResponse>(cancellationToken))!;

        // Assert
        Assert.True(getResult.IsSuccess);
        Assert.NotNull(getResult.Value);
        Assert.Equal(2, getResult.Value.MediaAssets.Count);

        var returnedIds = getResult.Value.MediaAssets.Select(a => a.Id).ToList();
        Assert.Contains(existingAsset1.Id, returnedIds);
        Assert.Contains(existingAsset2.Id, returnedIds);
        Assert.DoesNotContain(nonExistentId, returnedIds);
    }

    [Fact]
    public async Task GetMediaAssetsInfo_WithNonExistentIds_ShouldReturnEmptyList()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var nonExistentId1 = Guid.NewGuid();
        var nonExistentId2 = Guid.NewGuid();
        List<Guid> nonExistentIds = [nonExistentId1, nonExistentId2];
        var getMediaAssetsInfoRequest = new GetMediaAssetsRequest(nonExistentIds);

        // Act
        HttpResponseMessage getResponse = await AppHttpClient
            .PostAsJsonAsync("/files/batch", getMediaAssetsInfoRequest, cancellationToken);

        Result<GetMediaAssetsResponse, Failure> getResult = (await getResponse
            .HandleResponseAsync<GetMediaAssetsResponse>(cancellationToken))!;

        // Assert
        Assert.True(getResult.IsSuccess);
        Assert.NotNull(getResult.Value);
        Assert.Empty(getResult.Value.MediaAssets);
    }

    [Fact]
    public async Task GetMediaAssetsInfo_WithDuplicateIds_ShouldReturnUniqueAssets()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var asset = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken: cancellationToken);

        List<Guid> duplicateIds = [asset.Id, asset.Id];
        var getMediaAssetsInfoRequest = new GetMediaAssetsRequest(duplicateIds);

        // Act
        HttpResponseMessage getResponse = await AppHttpClient
            .PostAsJsonAsync("/files/batch", getMediaAssetsInfoRequest, cancellationToken);

        Result<GetMediaAssetsResponse, Failure> getResult = (await getResponse
            .HandleResponseAsync<GetMediaAssetsResponse>(cancellationToken))!;

        // Assert
        Assert.True(getResult.IsSuccess);
        Assert.NotNull(getResult.Value);
        Assert.Single(getResult.Value.MediaAssets);
        Assert.Equal(asset.Id, getResult.Value.MediaAssets[0].Id);
    }
}
