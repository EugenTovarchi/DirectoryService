using System.Net.Http.Json;
using Amazon.S3.Model;
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

public class CancelMultipartUploadTests : FileServiceBaseTests
{
    private readonly FileServiceTestWebFactory _factory;

    public CancelMultipartUploadTests(FileServiceTestWebFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cancel_UploadFiles_With_Valid_Data_Should_Succeed()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));

        StartMultipartUploadResponse startMultipartUploadResponse =
            await StartMultipartUpload(fileInfo, cancellationToken);

        // Act
        var cancelRequest = new CancelMultipartUploadRequest(startMultipartUploadResponse.MediaAssetId,
            startMultipartUploadResponse.UploadId);

        HttpResponseMessage cancelResponse = await AppHttpClient
            .PostAsJsonAsync("/files/multipart/cancel", cancelRequest, cancellationToken);

        var cancelResult = await cancelResponse.HandleResponseAsync(cancellationToken);

        // Assert
        var uploadingFilesInS3 = await CheckMultipartUploadNotExistsInS3(
            VideoAsset.LOCATION,
            fileInfo.Name,
            startMultipartUploadResponse.UploadId,
            cancellationToken);

        Assert.True(cancelResult.IsSuccess);
        Assert.Empty(uploadingFilesInS3!);

        await ExecuteInDb(async dbContext =>
        {
            var mediaAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == startMultipartUploadResponse.MediaAssetId, cancellationToken);

            Assert.Null(mediaAsset);
        });
    }

    [Fact]
    public async Task Cancel_WithNonExistentMediaAsset_Should_Fail()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var cancelRequest = new CancelMultipartUploadRequest(nonExistentId, "some-upload-id");
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        // Act
        HttpResponseMessage cancelResponse = await AppHttpClient
            .PostAsJsonAsync("/files/multipart/cancel", cancelRequest, cancellationToken);

        var cancelResult = await cancelResponse.HandleResponseAsync(CancellationToken.None);

        // Assert
        Assert.True(cancelResult.IsFailure);
    }

    [Fact]
    public async Task Cancel_WithInvalidUploadId_Should_Fail()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));

        StartMultipartUploadResponse startResponse = await StartMultipartUpload(fileInfo, cancellationToken);

        var cancelRequest = new CancelMultipartUploadRequest(
            startResponse.MediaAssetId,
            "invalid-upload-id");

        // Act
        HttpResponseMessage cancelResponse = await AppHttpClient
            .PostAsJsonAsync("/files/multipart/cancel", cancelRequest, cancellationToken);

        var cancelResult = await cancelResponse.HandleResponseAsync(cancellationToken);

        // Assert
        Assert.True(cancelResult.IsFailure);

        await ExecuteInDb(async dbContext =>
        {
            var mediaAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == startResponse.MediaAssetId, cancellationToken);

            Assert.NotNull(mediaAsset);
            Assert.Equal(MediaStatus.UPLOADING, mediaAsset.Status);
        });
    }

    private async Task<List<MultipartUpload>?> CheckMultipartUploadNotExistsInS3(
        string bucketName,
        string key,
        string uploadId,
        CancellationToken cancellationToken)
    {
        var fileStorageProvider = _factory.Services.GetRequiredService<IFileStorageProvider>();

        var storageKeyResult = StorageKey.Create(key, null, bucketName);
        Assert.True(storageKeyResult.IsSuccess);

        var listUploadsResult = await fileStorageProvider.FileListMultipartUploadAsync(
            storageKeyResult.Value,
            cancellationToken);

        Assert.True(listUploadsResult.IsSuccess);

        var multipartUploads = listUploadsResult.Value.MultipartUploads ?? [];
        if (multipartUploads.Count == 0)
        {
            return multipartUploads;
        }

        var matchingUpload = multipartUploads
            .Where(u => u.UploadId == uploadId)
            .ToList();

        return matchingUpload;
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
            TEST_OWNER_TYPE,
            TEST_DEPARTMENT_ID);

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