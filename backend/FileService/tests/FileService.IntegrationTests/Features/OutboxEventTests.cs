using System.Net.Http.Json;
using FileService.Contracts;
using FileService.Contracts.Requests;
using FileService.Contracts.Responses;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FileService.IntegrationTests.Features;

public class OutboxEventTests : FileServiceBaseTests
{
    public OutboxEventTests(FileServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CompleteMultipartUpload_Should_Publish_FileUploaded_Event()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));

        var startResponse = await StartMultipartUpload(fileInfo, cancellationToken);
        var partETags = await UploadChunks(fileInfo, startResponse, cancellationToken);

        // Act
        var completeRequest = new CompleteMultipartUploadRequest(
            startResponse.MediaAssetId,
            startResponse.UploadId,
            partETags.ToList());

        var completeResponse =
            await AppHttpClient.PostAsJsonAsync("/files/multipart/end", completeRequest, cancellationToken);
        completeResponse.EnsureSuccessStatusCode();

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            await Task.Delay(300, cancellationToken);

            var mediaAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == startResponse.MediaAssetId, cancellationToken);

            Assert.NotNull(mediaAsset);
            Assert.Equal(MediaStatus.UPLOADED, mediaAsset.Status);
            Assert.Equal(TEST_DEPARTMENT_ID, mediaAsset.OwnerId);
            Assert.Equal(TEST_OWNER_TYPE, mediaAsset.OwnerType);
        });
    }

    [Fact]
    public async Task DeleteFile_Should_Publish_FileDeleted_Event()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken: cancellationToken);

        // Act
        var deleteResponse = await AppHttpClient.DeleteAsync($"/files/{mediaAsset.Id}", cancellationToken);
        deleteResponse.EnsureSuccessStatusCode();

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            await Task.Delay(300, cancellationToken);

            var deletedAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == mediaAsset.Id, cancellationToken);

            Assert.NotNull(deletedAsset);
            Assert.Equal(MediaStatus.DELETED, deletedAsset.Status);
        });
    }

    private async Task<StartMultipartUploadResponse> StartMultipartUpload(
        FileInfo fileInfo,
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

        var response = await AppHttpClient.PostAsJsonAsync("/files/multipart/start", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<StartMultipartUploadResponse>(cancellationToken))!;
    }

    private async Task<IReadOnlyList<PartETagDto>> UploadChunks(
        FileInfo fileInfo,
        StartMultipartUploadResponse startResponse,
        CancellationToken cancellationToken)
    {
        var parts = new List<PartETagDto>();
        await using Stream fileStream = fileInfo.OpenRead();

        foreach (var chunkUrl in startResponse.ChunkUploadUrls.OrderBy(c => c.PartNumber))
        {
            byte[] chunk = new byte[startResponse.ChunkSize];
            int bytesRead = await fileStream.ReadAsync(chunk.AsMemory(0, startResponse.ChunkSize), cancellationToken);
            if (bytesRead == 0) break;

            var content = new ByteArrayContent(chunk, 0, bytesRead);
            var response = await HttpClient.PutAsync(chunkUrl.UploadUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            string? etag = response.Headers.ETag?.ToString().Trim('"');
            parts.Add(new PartETagDto(chunkUrl.PartNumber, etag!));
        }

        return parts;
    }
}