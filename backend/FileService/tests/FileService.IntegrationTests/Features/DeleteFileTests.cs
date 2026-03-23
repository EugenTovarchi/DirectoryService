using System.Net;
using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedService.Framework.ControllersResults;
using SharedService.SharedKernel;

namespace FileService.IntegrationTests.Features;

public class DeleteFileTests : FileServiceBaseTests
{
    private readonly FileServiceTestWebFactory _factory;

    public DeleteFileTests(FileServiceTestWebFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeleteFile_With_Valid_Data_Should_Succeed()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));

        var mediaAsset = await StartMultipartUpload(fileInfo, cancellationToken);
        var mediaAssetId = mediaAsset.MediaAssetId;

        // Act
        string deleteUrl = $"/files/{mediaAssetId}";

        HttpResponseMessage deleteResponse = await AppHttpClient
            .DeleteAsync(deleteUrl, cancellationToken);

        Result<Guid, Failure> deleteResult = await deleteResponse
            .HandleResponseAsync<Guid>(cancellationToken);

        // Assert
        Assert.True(deleteResult.IsSuccess);
        Assert.Equal(mediaAssetId, deleteResult.Value);

        await ExecuteInDb(async dbContext =>
        {
            var mediaAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == mediaAssetId, cancellationToken);

            var fileStorageProvider = _factory.Services.GetRequiredService<IFileStorageProvider>();
            var storageKey = mediaAsset!.UploadKey;

            var fileExistsResult = await fileStorageProvider.FileExistsAsync(storageKey, cancellationToken);

            Assert.True(fileExistsResult.IsSuccess);
            Assert.False(fileExistsResult.Value);

            Assert.NotNull(mediaAsset);
            Assert.Equal(MediaStatus.DELETED, mediaAsset.Status);
        });
    }

    [Fact]
    public async Task DeleteFile_With_NonExistent_Id_Should_Return_NotFound()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        Guid nonExistentId = Guid.NewGuid();

        // Act
        string deleteUrl = $"/files/{nonExistentId}";
        HttpResponseMessage deleteResponse = await AppHttpClient
            .DeleteAsync(deleteUrl, cancellationToken);

        Result<Guid, Failure> deleteResult = await deleteResponse
            .HandleResponseAsync<Guid>(cancellationToken);

        // Assert
        Assert.True(deleteResult.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_When_File_Already_Deleted_Should_Return_Error()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.UPLOADING, cancellationToken);

        Guid mediaAssetId = mediaAsset.Id;
        string deleteUrl = $"/files/{mediaAssetId}";

        HttpResponseMessage firstDeleteResponse = await AppHttpClient
            .DeleteAsync(deleteUrl, cancellationToken);

        // Act
        HttpResponseMessage secondDeleteResponse = await AppHttpClient
            .DeleteAsync(deleteUrl, cancellationToken);

        Result<Guid, Failure> secondDeleteResult = await secondDeleteResponse
            .HandleResponseAsync<Guid>(cancellationToken);

        // Assert
        Assert.True(secondDeleteResult.IsFailure);
        Assert.Equal(HttpStatusCode.BadRequest, secondDeleteResponse.StatusCode);

        await ExecuteInDb(async dbContext =>
        {
            var mediaAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == mediaAssetId, cancellationToken);

            Assert.NotNull(mediaAsset);
            Assert.Equal(MediaStatus.DELETED, mediaAsset.Status);
        });
    }

    public async Task<StartMultipartUploadResponse> StartMultipartUpload(FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        await CreateTestBucketAsync(VideoAsset.LOCATION);

        var request = new StartMultipartUploadRequest(
            fileInfo.Name,
            "video",
            "video/mp4",
            fileInfo.Length,
            "fileservice_test",
            Guid.Empty);

        HttpResponseMessage startMultipartUploadResponse =
            await AppHttpClient.PostAsJsonAsync("/files/multipart/start", request, cancellationToken);

        startMultipartUploadResponse.EnsureSuccessStatusCode();

        Result<StartMultipartUploadResponse, Failure> startMultipartUploadResult =
            await startMultipartUploadResponse.HandleResponseAsync<StartMultipartUploadResponse>(cancellationToken);

        Assert.NotNull(startMultipartUploadResult.Value);
        Assert.NotNull(startMultipartUploadResult.Value.UploadId);
        await ExecuteInDb(async dbContext =>
        {
            var mediaAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == startMultipartUploadResult.Value.MediaAssetId, cancellationToken);

            Assert.Equal(MediaStatus.UPLOADING, mediaAsset?.Status);
            Assert.NotNull(mediaAsset);
        });

        return startMultipartUploadResult.Value;
    }
}