using System.Net.Http.Json;
using Amazon.S3.Model;
using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Core.FilesStorage;
using FileService.Core.HttpCommunication;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedService.SharedKernel;

namespace FileService.IntegrationTests.Features;

public class GetChunkUploadUrlTests : FileServiceBaseTests
{
    private readonly FileServiceTestWebFactory _factory;

    public GetChunkUploadUrlTests(FileServiceTestWebFactory factory)
        : base(factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetChunkUploadUrl_With_Valid_Data_Should_Succeed()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));

        StartMultipartUploadResponse startMultipartUploadResponse =
            await StartMultipartUpload(fileInfo, cancellationToken);

        // Act
        var getChunkUploadUrlRequest = new GetChunkUploadUrlRequest(startMultipartUploadResponse.MediaAssetId,
            startMultipartUploadResponse.UploadId, 1);

        HttpResponseMessage getChunkUploadUrlResponse = await AppHttpClient
            .PostAsJsonAsync("/files/chunk-upload/url", getChunkUploadUrlRequest, cancellationToken);

        Result<GetChunkUploadUrlResponse, Failure> getChunkUrlResult = await getChunkUploadUrlResponse
            .HandleResponseAsync<GetChunkUploadUrlResponse>(cancellationToken);

        // Assert
        Assert.True(getChunkUrlResult.IsSuccess);
        Assert.NotNull(getChunkUrlResult.Value);
        Assert.Equal(getChunkUploadUrlRequest.PartNumber, getChunkUrlResult.Value.PartNumber);

        await ExecuteInDb(async dbContext =>
        {
            var mediaAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == startMultipartUploadResponse.MediaAssetId, cancellationToken);

            var uploadingFilesInS3 = await CheckMultipartUploadExistsInS3(
                mediaAsset!.Key,
                startMultipartUploadResponse.UploadId,
                cancellationToken);

            Assert.NotEmpty(uploadingFilesInS3!);
            Assert.NotNull(mediaAsset);

            string firstChunkUrlFromStart = startMultipartUploadResponse.ChunkUploadUrls[0].ToString();
            string firstChunkUrlFromGet = getChunkUrlResult.Value.UploadUrl;
            Assert.NotEqual(firstChunkUrlFromGet, firstChunkUrlFromStart);
        });
    }

    [Fact]
    public async Task GetChunkUploadUrl_With_Invalid_PartNumber_Should_Be_Failure()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));

        StartMultipartUploadResponse startMultipartUploadResponse =
            await StartMultipartUpload(fileInfo, cancellationToken);

        const int invalidPartNumber = 1000;

        // Act
        var getChunkUploadUrlRequest = new GetChunkUploadUrlRequest(startMultipartUploadResponse.MediaAssetId,
            startMultipartUploadResponse.UploadId, invalidPartNumber);

        HttpResponseMessage getChunkUploadUrlResponse = await AppHttpClient
            .PostAsJsonAsync("/files/chunk-upload/url", getChunkUploadUrlRequest, cancellationToken);

        Result<GetChunkUploadUrlResponse, Failure> getChunkUrlResult = await getChunkUploadUrlResponse
            .HandleResponseAsync<GetChunkUploadUrlResponse>(cancellationToken);

        // Assert
        Assert.True(getChunkUrlResult.IsFailure);
    }

    [Fact]
    public async Task GetChunkUploadUrl_When_Uploading_Not_Exist_Should_Be_Failure()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));

        StartMultipartUploadResponse startMultipartUploadResponse =
            await StartMultipartUpload(fileInfo, cancellationToken);

        var fileStorageProvider = _factory.Services.GetRequiredService<IFileStorageProvider>();
        await ExecuteInDb(async dbContext =>
        {
            var mediaAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == startMultipartUploadResponse.MediaAssetId, cancellationToken);

            var abortResult = await fileStorageProvider.AbortMultipartUploadAsync(
                mediaAsset!.Key,
                startMultipartUploadResponse.UploadId,
                cancellationToken);

            Assert.True(abortResult.IsSuccess);
        });

        // Act
        var getChunkUploadUrlRequest = new GetChunkUploadUrlRequest(startMultipartUploadResponse.MediaAssetId,
            startMultipartUploadResponse.UploadId, 1);

        HttpResponseMessage getChunkUploadUrlResponse = await AppHttpClient
            .PostAsJsonAsync("/files/chunk-upload/url", getChunkUploadUrlRequest, cancellationToken);

        Result<GetChunkUploadUrlResponse, Failure> getChunkUrlResult = await getChunkUploadUrlResponse
            .HandleResponseAsync<GetChunkUploadUrlResponse>(cancellationToken);

        // Assert
        Assert.True(getChunkUrlResult.IsFailure);
    }

    public async Task<StartMultipartUploadResponse> StartMultipartUpload(FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        string bucketName = await CreateTestBucketAsync(VideoAsset.LOCATION);

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

    private async Task<List<MultipartUpload>?> CheckMultipartUploadExistsInS3(
        StorageKey mediaAssetKey,
        string uploadId,
        CancellationToken cancellationToken)
    {
        var fileStorageProvider = _factory.Services.GetRequiredService<IFileStorageProvider>();

        var listUploadsResult = await fileStorageProvider.FileListMultipartUploadAsync(
            mediaAssetKey,
            cancellationToken);

        Assert.True(listUploadsResult.IsSuccess);

        var multipartUploads = listUploadsResult.Value.MultipartUploads ?? [];
        if (multipartUploads.Count == 0)
        {
            return multipartUploads;
        }

        var matchingUpload = multipartUploads
            .Where(u => u.UploadId == uploadId
                        && u.Key == mediaAssetKey.Value)
            .ToList();

        Assert.NotNull(matchingUpload);

        return matchingUpload;
    }
}