using System.Net.Http.Json;
using Amazon.S3.Model;
using CSharpFunctionalExtensions;
using FileService.Contracts;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing;
using FileService.IntegrationTests.Infrastructure;
using FileService.VideoProcessing.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedService.Framework.ControllersResults;
using SharedService.SharedKernel;
using CompleteMultipartUploadRequest = FileService.Contracts.Requests.CompleteMultipartUploadRequest;

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

        Guid videoAssetId = await UploadTestVideoAsync(cancellationToken);

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

            bool rawKeyExists = listResponse.S3Objects.Any(o => o.Key == rawKey);
            Assert.False(rawKeyExists, $"Raw file with key '{rawKey}' not founded");
        });
    }

    private async Task<Guid> UploadTestVideoAsync(CancellationToken cancellationToken)
    {
        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));
        StartMultipartUploadResponse startResponse = await StartMultipartUpload(fileInfo, cancellationToken);

        IReadOnlyCollection<PartETagDto> partETag = await UploadChunks(fileInfo, startResponse, cancellationToken);
        await CompleteMultipartUpload(startResponse, partETag, cancellationToken);
        return startResponse.MediaAssetId;
    }

    public async Task<StartMultipartUploadResponse> StartMultipartUpload(FileInfo fileInfo,
        CancellationToken cancellationToken)
    {
        await CreateTestBucketAsync(VideoAsset.LOCATION);
        await CreateTestBucketAsync(PreviewAsset.LOCATION);

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

        await ExecuteInDb(async dbContext =>
        {
            await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == startMultipartUploadResult.Value.MediaAssetId, cancellationToken);
        });

        return startMultipartUploadResult.Value;
    }

    private async Task<IReadOnlyList<PartETagDto>> UploadChunks(
        FileInfo fileInfo,
        StartMultipartUploadResponse startMultipartUploadResponse,
        CancellationToken cancellationToken)
    {
        var parts = new List<PartETagDto>();

        await using Stream fileStream = fileInfo.OpenRead();
        foreach (ChunkUploadUrl chunkUploadUrl in
                 startMultipartUploadResponse.ChunkUploadUrls.OrderBy(c => c.PartNumber))
        {
            byte[] chunk = new byte [startMultipartUploadResponse.ChunkSize];
            int bytesRead = await fileStream.ReadAsync(chunk.AsMemory(
                0, startMultipartUploadResponse.ChunkSize), cancellationToken);
            if (bytesRead == 0)
                break;

            var content = new ByteArrayContent(chunk, 0, bytesRead);

            var response = await HttpClient.PutAsync(chunkUploadUrl.UploadUrl, content, cancellationToken);

            response.EnsureSuccessStatusCode();

            string? etag = response.Headers.ETag?.ToString().Trim('"');

            parts.Add(new PartETagDto(chunkUploadUrl.PartNumber, etag!));
        }

        return parts;
    }

    private async Task CompleteMultipartUpload(StartMultipartUploadResponse startMultipartUploadResponse,
        IEnumerable<PartETagDto> partETags,
        CancellationToken cancellationToken)
    {
        var completeRequest = new CompleteMultipartUploadRequest(
            startMultipartUploadResponse.MediaAssetId,
            startMultipartUploadResponse.UploadId,
            partETags.ToList());

        var completeResponse = await AppHttpClient
            .PostAsJsonAsync("/files/multipart/end", completeRequest, cancellationToken);

        await completeResponse.HandleResponseAsync(cancellationToken);
    }
}