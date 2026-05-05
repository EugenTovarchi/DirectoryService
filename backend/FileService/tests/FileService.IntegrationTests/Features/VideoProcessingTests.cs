using Amazon.S3.Model;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing;
using FileService.IntegrationTests.Infrastructure;
using FileService.VideoProcessing.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileService.IntegrationTests.Features;

public class VideoProcessingTests : FileServiceBaseTests
{
    public VideoProcessingTests(FileServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ProcessingVideo_WhenValidVideoUploaded_ShouldCompleteProcessingSuccessfully()
    {
        // Arrange
        using var ct = new CancellationTokenSource();
        CancellationToken cancellationToken = ct.Token;

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        IVideoProcessingService videoProcessingService =
            scope.ServiceProvider.GetRequiredService<IVideoProcessingService>();

        await CreateTestBucketAsync(VideoAsset.LOCATION);
        await CreateTestBucketAsync(PreviewAsset.LOCATION);

        var uploadedVideo = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken: cancellationToken);
        Guid videoAssetId = uploadedVideo.Id;

        // Act
        var result = await videoProcessingService.ProcessVideoAsync(videoAssetId, cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);

        MediaAsset? mediaAsset = null;
        string? rawKey = null;

        await ExecuteInDb(async dbContext =>
        {
            mediaAsset = await dbContext.MediaAssets
                .AsNoTracking()
                .FirstOrDefaultAsync(ma => ma.Id == videoAssetId, cancellationToken);

            var videoProcess = await dbContext.VideoProcesses
                .AsNoTracking()
                .FirstOrDefaultAsync(vp => vp.VideoAssetId == videoAssetId, cancellationToken);

            Assert.NotNull(mediaAsset);
            Assert.Equal(MediaStatus.READY, mediaAsset.Status);
            Assert.Equal(TEST_DEPARTMENT_ID, mediaAsset.OwnerId);
            Assert.Equal(TEST_OWNER_TYPE, mediaAsset.OwnerType);

            Assert.NotNull(mediaAsset.Key);
            Assert.Equal($"hls/{videoAssetId}/{VideoAsset.MASTER_PLAYLIST_NAME}", mediaAsset.Key.Value);

            VideoAsset? videoAsset = mediaAsset as VideoAsset;
            Assert.NotNull(videoAsset);
            Assert.NotNull(videoAsset.RawKey);
            rawKey = videoAsset.RawKey.Value;

            Assert.NotNull(videoProcess);
            Assert.Equal(VideoProcessStatus.SUCCEEDED, videoProcess.Status);
        });

        await ExecuteInS3(async s3Client =>
        {
            StorageKey key = mediaAsset?.Key ?? throw new InvalidOperationException("Media Asset Key is null");
            string prefix = key.Prefix;

            var listRequest = new ListObjectsV2Request { BucketName = VideoAsset.LOCATION, Prefix = prefix };

            var listResponse = await s3Client.ListObjectsV2Async(listRequest, cancellationToken);

            Assert.NotEmpty(listResponse.S3Objects);

            GetObjectMetadataResponse? objectData = await s3Client.GetObjectMetadataAsync(
                VideoAsset.LOCATION, key.Value, cancellationToken);

            Assert.NotNull(objectData);

            bool rawKeyExists = listResponse.S3Objects.Any(o =>
                string.Equals(o.Key, rawKey, StringComparison.Ordinal));
            Assert.False(rawKeyExists, $"Raw file with key '{rawKey}' not found");
        });
    }
}
