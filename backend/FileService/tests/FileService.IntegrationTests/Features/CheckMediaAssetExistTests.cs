using CSharpFunctionalExtensions;
using FileService.Contracts.Responses;
using FileService.Domain;
using FileService.IntegrationTests.Infrastructure;
using SharedService.Framework.ControllersResults;
using SharedService.SharedKernel;

namespace FileService.IntegrationTests.Features;

public class CheckMediaAssetExistTests : FileServiceBaseTests
{
    public CheckMediaAssetExistTests(FileServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Theory]
    [InlineData(MediaStatus.UPLOADED, true)]
    [InlineData(MediaStatus.READY, true)]
    [InlineData(MediaStatus.DELETED, false)]
    [InlineData(MediaStatus.UPLOADING, false)]
    [InlineData(MediaStatus.PROCESSING, false)]
    [InlineData(MediaStatus.FAILED, false)]
    public async Task CheckMediaAssetExist_Should_Return_Usable_Status_Only(
        MediaStatus status,
        bool expectedIsExist)
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        var mediaAsset = await CreateVideoAssetAsync(status, cancellationToken: cancellationToken);

        // Act
        HttpResponseMessage response = await AppHttpClient.PostAsync(
            $"/files/{mediaAsset.Id}/exists",
            null,
            cancellationToken);

        Result<CheckMediaAssetExistResponse, Failure> result =
            await response.HandleResponseAsync<CheckMediaAssetExistResponse>(cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedIsExist, result.Value.IsExist);
    }

    [Fact]
    public async Task CheckMediaAssetExist_With_NonExistent_Id_Should_Return_False()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        Guid mediaAssetId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await AppHttpClient.PostAsync(
            $"/files/{mediaAssetId}/exists",
            null,
            cancellationToken);

        Result<CheckMediaAssetExistResponse, Failure> result =
            await response.HandleResponseAsync<CheckMediaAssetExistResponse>(cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsExist);
    }
}
