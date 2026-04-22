using System.Net;
using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using FileService.Contracts;
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
    public async Task DeleteFile_FullCycle_Should_Succeed()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));

        var startResponse = await StartMultipartUpload(fileInfo, cancellationToken);

        var partETags = await UploadChunks(fileInfo, startResponse, cancellationToken);

        await CompleteMultipartUpload(startResponse, partETags, cancellationToken);

        var mediaAssetId = startResponse.MediaAssetId;

        // Act
        string deleteUrl = $"/files/{mediaAssetId}";
        HttpResponseMessage deleteResponse = await AppHttpClient.DeleteAsync(deleteUrl, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        Result<Guid, Failure> deleteResult = await deleteResponse
            .HandleResponseAsync<Guid>(cancellationToken);

        Assert.True(deleteResult.IsSuccess);
        Assert.Equal(mediaAssetId, deleteResult.Value);

        await ExecuteInDb(async dbContext =>
        {
            var deletedAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == mediaAssetId, cancellationToken);

            var fileStorageProvider = _factory.Services.GetRequiredService<IFileStorageProvider>();
            var storageKey = deletedAsset!.UploadKey;

            var fileExistsResult = await fileStorageProvider.FileExistsAsync(storageKey, cancellationToken);

            Assert.True(fileExistsResult.IsSuccess);
            Assert.False(fileExistsResult.Value);
            Assert.NotNull(deletedAsset);
            Assert.Equal(MediaStatus.DELETED, deletedAsset.Status);
        });
    }

    [Fact]
    public async Task DeleteFile_With_Uploaded_Status_Should_Succeed()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        // Создаём уже загруженный файл (UPLOADED)
        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken: cancellationToken);
        var mediaAssetId = mediaAsset.Id;

        // Act
        string deleteUrl = $"/files/{mediaAssetId}";
        HttpResponseMessage deleteResponse = await AppHttpClient.DeleteAsync(deleteUrl, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        Result<Guid, Failure> deleteResult = await deleteResponse
            .HandleResponseAsync<Guid>(cancellationToken);

        Assert.True(deleteResult.IsSuccess);
        Assert.Equal(mediaAssetId, deleteResult.Value);

        await ExecuteInDb(async dbContext =>
        {
            var deletedAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == mediaAssetId, cancellationToken);

            Assert.NotNull(deletedAsset);
            Assert.Equal(MediaStatus.DELETED, deletedAsset.Status);
        });
    }

    [Fact]
    public async Task DeleteFile_With_Uploading_Status_Should_Return_Error()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        // Файл в статусе UPLOADING — НЕЛЬЗЯ удалять через DeleteFile
        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.UPLOADING, cancellationToken: cancellationToken);
        var mediaAssetId = mediaAsset.Id;

        // Act
        string deleteUrl = $"/files/{mediaAssetId}";
        HttpResponseMessage deleteResponse = await AppHttpClient.DeleteAsync(deleteUrl, cancellationToken);

        // Assert — должно быть BadRequest
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        await ExecuteInDb(async dbContext =>
        {
            var asset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == mediaAssetId, cancellationToken);

            // Файл НЕ удалён, статус остался UPLOADING
            Assert.NotNull(asset);
            Assert.Equal(MediaStatus.UPLOADING, asset.Status);
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
        HttpResponseMessage deleteResponse = await AppHttpClient.DeleteAsync(deleteUrl, cancellationToken);

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

        // Создаём и удаляем файл
        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.UPLOADED, cancellationToken: cancellationToken);
        Guid mediaAssetId = mediaAsset.Id;
        string deleteUrl = $"/files/{mediaAssetId}";

        // Первое удаление — успешно
        var firstDeleteResponse = await AppHttpClient.DeleteAsync(deleteUrl, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstDeleteResponse.StatusCode);

        // Act — второе удаление
        HttpResponseMessage secondDeleteResponse = await AppHttpClient.DeleteAsync(deleteUrl, cancellationToken);

        Result<Guid, Failure> secondDeleteResult = await secondDeleteResponse
            .HandleResponseAsync<Guid>(cancellationToken);

        // Assert
        Assert.True(secondDeleteResult.IsFailure);
        Assert.Equal(HttpStatusCode.BadRequest, secondDeleteResponse.StatusCode);

        await ExecuteInDb(async dbContext =>
        {
            var deletedAsset = await dbContext.MediaAssets
                .FirstOrDefaultAsync(m => m.Id == mediaAssetId, cancellationToken);

            Assert.NotNull(deletedAsset);
            Assert.Equal(MediaStatus.DELETED, deletedAsset.Status);
        });
    }

    [Fact]
    public async Task DeleteFile_With_Ready_Status_Should_Succeed()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.READY, cancellationToken: cancellationToken);
        var mediaAssetId = mediaAsset.Id;

        // Act
        string deleteUrl = $"/files/{mediaAssetId}";
        HttpResponseMessage deleteResponse = await AppHttpClient.DeleteAsync(deleteUrl, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteFile_With_Failed_Status_Should_Succeed()
    {
        // Arrange
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        var mediaAsset = await CreateVideoAssetAsync(MediaStatus.FAILED, cancellationToken: cancellationToken);
        var mediaAssetId = mediaAsset.Id;

        // Act
        string deleteUrl = $"/files/{mediaAssetId}";
        HttpResponseMessage deleteResponse = await AppHttpClient.DeleteAsync(deleteUrl, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }

    public async Task<StartMultipartUploadResponse> StartMultipartUpload(
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

        HttpResponseMessage response = await AppHttpClient
            .PostAsJsonAsync("/files/multipart/start", request, cancellationToken);

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

    private async Task CompleteMultipartUpload(
        StartMultipartUploadResponse startResponse,
        IEnumerable<PartETagDto> partETags,
        CancellationToken cancellationToken)
    {
        var completeRequest = new CompleteMultipartUploadRequest(
            startResponse.MediaAssetId,
            startResponse.UploadId,
            partETags.ToList());

        var completeResponse = await AppHttpClient
            .PostAsJsonAsync("/files/multipart/end", completeRequest, cancellationToken);

        completeResponse.EnsureSuccessStatusCode();
    }
}