using System.Net.Http.Json;
using Amazon.S3.Model;
using CSharpFunctionalExtensions;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.Uploads;
using FileService.Infrastructure.Postgres.Background;
using FileService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

            var uploadSession = await dbContext.MultipartUploadSessions
                .FirstOrDefaultAsync(
                    session => session.MediaAssetId == startMultipartUploadResponse.MediaAssetId,
                    cancellationToken);

            Assert.NotNull(mediaAsset);
            Assert.Equal(MediaStatus.FAILED, mediaAsset.Status);
            Assert.NotNull(uploadSession);
            Assert.Equal(MultipartUploadStatus.ABORTED, uploadSession.Status);
        });
    }

    [Fact]
    public async Task CleanupExpiredUpload_ShouldAbortStorageAndKeepAuditState()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));
        StartMultipartUploadResponse startResponse = await StartMultipartUpload(fileInfo, cancellationToken);

        await ExecuteInDb(dbContext => dbContext.MultipartUploadSessions
            .Where(session => session.MediaAssetId == startResponse.MediaAssetId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    session => session.ExpiresAt,
                    DateTime.UtcNow.AddMinutes(-1)),
                cancellationToken));

        MultipartUploadCleanupService cleanupService = _factory.Services
            .GetServices<IHostedService>()
            .OfType<MultipartUploadCleanupService>()
            .Single();

        // Act
        await cleanupService.CleanupExpiredUploadsAsync(cancellationToken);

        // Assert
        List<MultipartUpload>? uploads = await CheckMultipartUploadNotExistsInS3(
            VideoAsset.LOCATION,
            fileInfo.Name,
            startResponse.UploadId,
            cancellationToken);

        Assert.Empty(uploads!);
        await ExecuteInDb(async dbContext =>
        {
            MediaAsset? mediaAsset = await dbContext.MediaAssets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    asset => asset.Id == startResponse.MediaAssetId,
                    cancellationToken);
            MultipartUploadSession? session = await dbContext.MultipartUploadSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    uploadSession => uploadSession.MediaAssetId == startResponse.MediaAssetId,
                    cancellationToken);

            Assert.NotNull(mediaAsset);
            Assert.Equal(MediaStatus.FAILED, mediaAsset.Status);
            Assert.NotNull(session);
            Assert.Equal(MultipartUploadStatus.EXPIRED, session.Status);
        });
    }

    [Fact]
    public async Task CleanupStaleAbortClaim_ShouldResumeCleanup()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));
        StartMultipartUploadResponse startResponse = await StartMultipartUpload(fileInfo, cancellationToken);

        await ExecuteInDb(dbContext => dbContext.MultipartUploadSessions
            .Where(session => session.MediaAssetId == startResponse.MediaAssetId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.Status, MultipartUploadStatus.ABORTING)
                    .SetProperty(session => session.ExpiresAt, DateTime.UtcNow.AddMinutes(-10))
                    .SetProperty(session => session.UpdatedAt, DateTime.UtcNow.AddMinutes(-10)),
                cancellationToken));

        MultipartUploadCleanupService cleanupService = _factory.Services
            .GetServices<IHostedService>()
            .OfType<MultipartUploadCleanupService>()
            .Single();

        // Act
        await cleanupService.CleanupExpiredUploadsAsync(cancellationToken);

        // Assert
        List<MultipartUpload>? uploads = await CheckMultipartUploadNotExistsInS3(
            VideoAsset.LOCATION,
            fileInfo.Name,
            startResponse.UploadId,
            cancellationToken);

        Assert.Empty(uploads!);
        await ExecuteInDb(async dbContext =>
        {
            MultipartUploadSession? session = await dbContext.MultipartUploadSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    uploadSession => uploadSession.MediaAssetId == startResponse.MediaAssetId,
                    cancellationToken);

            Assert.NotNull(session);
            Assert.Equal(MultipartUploadStatus.EXPIRED, session.Status);
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
            await SendStartMultipartUploadRequestAsync(request, cancellationToken);

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
