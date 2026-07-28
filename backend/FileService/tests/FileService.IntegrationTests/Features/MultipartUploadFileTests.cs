using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using FileService.Contracts;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing;
using FileService.Domain.Uploads;
using FileService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SharedService.Framework.ControllersResults;
using SharedService.SharedKernel;
using CompleteMultipartUploadRequest = FileService.Contracts.Requests.CompleteMultipartUploadRequest;

namespace FileService.IntegrationTests.Features;

public class MultipartUploadFileTests : FileServiceBaseTests
{
    public MultipartUploadFileTests(FileServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task MultipartUploadFiles_FullCycle_With_Valid_Data_Should_Succeed()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        CancellationToken cancellationToken = cancellationTokenSource.Token;

        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));
        await CreateTestBucketAsync(PreviewAsset.LOCATION);

        // Act
        var startMultipartUploadResponse = await StartMultipartUpload(fileInfo, cancellationToken);

        IReadOnlyList<PartETagDto> partEtags =
            await UploadChunks(fileInfo, startMultipartUploadResponse, cancellationToken);

        var result = await CompleteMultipartUpload(startMultipartUploadResponse, partEtags, cancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        await WaitForVideoProcessingCompletionAsync(
            startMultipartUploadResponse.MediaAssetId,
            cancellationToken);

        await ExecuteInDb(async dbContext =>
        {
            var mediaAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == startMultipartUploadResponse.MediaAssetId, cancellationToken);

            Assert.NotNull(mediaAsset);

            Assert.Equal(MediaStatus.READY, mediaAsset.Status);

            var videoProcess = await dbContext.VideoProcesses
                .FirstOrDefaultAsync(v => v.VideoAssetId == mediaAsset.Id, cancellationToken);

            Assert.NotNull(videoProcess);
            Assert.Equal(VideoProcessStatus.SUCCEEDED, videoProcess.Status);
        });
    }

    [Fact]
    public async Task CompleteMultipartUpload_WhenCalledConcurrently_ShouldFinalizeOnce()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));

        StartMultipartUploadResponse startResponse = await StartMultipartUpload(fileInfo, cancellationToken);
        IReadOnlyList<PartETagDto> partEtags = await UploadChunks(fileInfo, startResponse, cancellationToken);
        var request = new CompleteMultipartUploadRequest(
            startResponse.MediaAssetId,
            startResponse.UploadId,
            partEtags);

        // Act
        Task<HttpResponseMessage> firstRequest = AppHttpClient
            .PostAsJsonAsync("/files/multipart/end", request, cancellationToken);
        Task<HttpResponseMessage> secondRequest = AppHttpClient
            .PostAsJsonAsync("/files/multipart/end", request, cancellationToken);

        HttpResponseMessage[] responses = await Task.WhenAll(firstRequest, secondRequest);
        UnitResult<Failure>[] results =
        [
            await responses[0].HandleResponseAsync(cancellationToken),
            await responses[1].HandleResponseAsync(cancellationToken)
        ];

        HttpResponseMessage retryResponse = await AppHttpClient
            .PostAsJsonAsync("/files/multipart/end", request, cancellationToken);
        UnitResult<Failure> retryResult = await retryResponse.HandleResponseAsync(cancellationToken);

        // Assert
        Assert.Contains(results, result => result.IsSuccess);
        Assert.True(retryResult.IsSuccess);

        await ExecuteInDb(async dbContext =>
        {
            MultipartUploadSession? session = await dbContext.MultipartUploadSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    uploadSession => uploadSession.MediaAssetId == startResponse.MediaAssetId,
                    cancellationToken);
            int videoProcessCount = await dbContext.VideoProcesses
                .AsNoTracking()
                .CountAsync(
                    process => process.VideoAssetId == startResponse.MediaAssetId,
                    cancellationToken);

            Assert.NotNull(session);
            Assert.Equal(MultipartUploadStatus.COMPLETED, session.Status);
            Assert.Equal(1, videoProcessCount);
        });
    }

    [Fact]
    public async Task StartMultipartUpload_WithSameIdempotencyKey_ShouldReuseSession()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));
        await CreateTestBucketAsync(VideoAsset.LOCATION);

        string idempotencyKey = Guid.NewGuid().ToString();
        var request = new StartMultipartUploadRequest(
            fileInfo.Name,
            "video",
            "video/mp4",
            fileInfo.Length,
            TEST_OWNER_TYPE,
            TEST_DEPARTMENT_ID);

        // Act
        Result<StartMultipartUploadResponse, Failure> firstResult =
            await SendStartRequestAsync(request, idempotencyKey, cancellationToken);
        Result<StartMultipartUploadResponse, Failure> retryResult =
            await SendStartRequestAsync(request, idempotencyKey, cancellationToken);
        Result<StartMultipartUploadResponse, Failure> conflictingResult =
            await SendStartRequestAsync(
                request with { FileName = "another-file.mp4" },
                idempotencyKey,
                cancellationToken);

        // Assert
        Assert.True(firstResult.IsSuccess);
        Assert.True(retryResult.IsSuccess);
        Assert.Equal(firstResult.Value.MediaAssetId, retryResult.Value.MediaAssetId);
        Assert.Equal(firstResult.Value.UploadId, retryResult.Value.UploadId);
        Assert.True(conflictingResult.IsFailure);

        await ExecuteInDb(async dbContext =>
        {
            int sessionCount = await dbContext.MultipartUploadSessions
                .AsNoTracking()
                .CountAsync(
                    session => session.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            Assert.Equal(1, sessionCount);
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
            byte[] chunk = new byte[startMultipartUploadResponse.ChunkSize];
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

    private async Task<UnitResult<Failure>> CompleteMultipartUpload(
        StartMultipartUploadResponse startMultipartUploadResponse,
        IEnumerable<PartETagDto> partETags,
        CancellationToken cancellationToken)
    {
        var completeRequest = new CompleteMultipartUploadRequest(
            startMultipartUploadResponse.MediaAssetId,
            startMultipartUploadResponse.UploadId,
            partETags.ToList());

        var completeResponse = await AppHttpClient
            .PostAsJsonAsync("/files/multipart/end", completeRequest, cancellationToken);

        UnitResult<Failure> completeResult = await completeResponse.HandleResponseAsync(cancellationToken);

        return completeResult;
    }

    private async Task<Result<StartMultipartUploadResponse, Failure>> SendStartRequestAsync(
        StartMultipartUploadRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/files/multipart/start")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        HttpResponseMessage response = await AppHttpClient.SendAsync(httpRequest, cancellationToken);
        return await response.HandleResponseAsync<StartMultipartUploadResponse>(cancellationToken);
    }

    private async Task WaitForVideoProcessingCompletionAsync(
        Guid videoAssetId,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            (MediaStatus? AssetStatus, VideoProcessStatus? ProcessStatus, string? ErrorMessage) state =
                await ExecuteInDb(async dbContext =>
                {
                    var mediaAsset = await dbContext.MediaAssets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(asset => asset.Id == videoAssetId, cancellationToken);
                    var videoProcess = await dbContext.VideoProcesses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(process => process.VideoAssetId == videoAssetId, cancellationToken);

                    return (mediaAsset?.Status, videoProcess?.Status, videoProcess?.ErrorMessage);
                });

            if (state.AssetStatus == MediaStatus.READY
                && state.ProcessStatus == VideoProcessStatus.SUCCEEDED)
            {
                return;
            }

            if (state.ProcessStatus == VideoProcessStatus.FAILED)
                Assert.Fail($"Video processing failed during integration test: {state.ErrorMessage}");

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        Assert.Fail($"Video processing did not complete for asset {videoAssetId} before timeout");
    }

}
