using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using FileService.Contracts;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing;
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
