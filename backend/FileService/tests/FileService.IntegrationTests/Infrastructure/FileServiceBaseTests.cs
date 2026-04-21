using Amazon.S3;
using FileService.Core.FilesStorage;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Infrastructure.Postgres;
using Microsoft.Extensions.DependencyInjection;

namespace FileService.IntegrationTests.Infrastructure;

[Collection("FileServiceCollection")]
public abstract class FileServiceBaseTests : IClassFixture<FileServiceTestWebFactory>, IAsyncLifetime
{
    private readonly Func<Task> _resetDatabase;
    private readonly IAmazonS3 _s3Client;

    public const string TEST_FILE_NAME = "test-file.mp4";
    public static readonly Guid TEST_DEPARTMENT_ID = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public const string TEST_OWNER_TYPE = "department";

    protected FileServiceBaseTests(FileServiceTestWebFactory factory)
    {
        AppHttpClient = factory.CreateClient();
        HttpClient = new HttpClient();
        Services = factory.Services;
        _s3Client = Services.GetRequiredService<IAmazonS3>();
        _resetDatabase = factory.ResetDatabaseAsync;
    }

    protected HttpClient HttpClient { get; init; }
    protected HttpClient AppHttpClient { get; init; }
    protected IServiceProvider Services { get; init; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _resetDatabase();
    }

    protected async Task<T> ExecuteInDb<T>(Func<FileServiceDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<FileServiceDbContext>();

        return await action(dbContext);
    }

    protected async Task ExecuteInDb(Func<FileServiceDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<FileServiceDbContext>();

        await action(dbContext);
    }

    protected async Task ExecuteInFileProvider(Func<IFileStorageProvider, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();

        var fileStorageProvider = scope.ServiceProvider.GetRequiredService<IFileStorageProvider>();

        await action(fileStorageProvider);
    }

    protected async Task ExecuteInS3(Func<IAmazonS3, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();

        var amazonS3 = scope.ServiceProvider.GetRequiredService<IAmazonS3>();

        await action(amazonS3);
    }

    protected async Task<string> CreateTestBucketAsync(string bucketName)
    {
        try
        {
            await _s3Client.PutBucketAsync(bucketName);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
        }

        return bucketName;
    }

    protected async Task<VideoAsset> CreateVideoAssetAsync(MediaStatus status,
        Guid? ownerId = null,
        string ownerType = TEST_OWNER_TYPE,
        CancellationToken cancellationToken = default)
    {
        var effectiveOwnerId = ownerId ?? TEST_DEPARTMENT_ID;

        VideoAsset videoAsset = await ExecuteInDb(async dbContext =>
        {
            Guid mediaAssetId = Guid.NewGuid();

            FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", TEST_FILE_NAME));

            var fileName = FileName.Create(TEST_FILE_NAME).Value;
            var contentType = ContentType.Create("video/mp4").Value;
            var mediaData = MediaData.Create(fileName, contentType, fileInfo.Length, 1).Value;

            VideoAsset videoAsset =
                VideoAsset.CreateForUpload(mediaAssetId, mediaData, effectiveOwnerId, ownerType).Value;

            dbContext.MediaAssets.Add(videoAsset);
            await dbContext.SaveChangesAsync(cancellationToken);

            await ExecuteInFileProvider(async fileProvider =>
            {
                await fileProvider.UploadFileAsync(
                    videoAsset.UploadKey,
                    fileInfo.OpenRead(),
                    mediaData.ContentType.Value,
                    cancellationToken);
            });

            if (status != MediaStatus.UPLOADING)
            {
                switch (status)
                {
                    case MediaStatus.FAILED:
                        videoAsset.MarkFailed();
                        break;

                    case MediaStatus.UPLOADED:
                        videoAsset.MarkUploaded();
                        break;

                    case MediaStatus.DELETED:
                        {
                            videoAsset.MarkUploaded();
                            videoAsset.MarkDeleted();
                            break;
                        }

                    case MediaStatus.READY:
                        {
                            videoAsset.MarkUploaded();
                            videoAsset.MarkReady();
                            break;
                        }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return videoAsset;
        });

        return videoAsset;
    }

    protected async Task<PhotoAsset> CreatePhotoAssetAsync(
        MediaStatus status,
        Guid? ownerId = null,
        string ownerType = TEST_OWNER_TYPE,
        CancellationToken cancellationToken = default)
    {
        var effectiveOwnerId = ownerId ?? TEST_DEPARTMENT_ID;
        const string photoFileName = "test-photo.jpg";

        PhotoAsset photoAsset = await ExecuteInDb(async dbContext =>
        {
            Guid mediaAssetId = Guid.NewGuid();
            FileInfo fileInfo = new(Path.Combine(AppContext.BaseDirectory, "Resources", photoFileName));

            var fileName = FileName.Create(photoFileName).Value;
            var contentType = ContentType.Create("image/jpeg").Value;
            var mediaData = MediaData.Create(fileName, contentType, fileInfo.Length, 1).Value;

            PhotoAsset asset = PhotoAsset.CreateForUpload(
                mediaAssetId,
                mediaData,
                effectiveOwnerId,
                ownerType).Value;

            dbContext.MediaAssets.Add(asset);
            await dbContext.SaveChangesAsync(cancellationToken);

            await ExecuteInFileProvider(async fileProvider =>
            {
                await fileProvider.UploadFileAsync(
                    asset.UploadKey,
                    fileInfo.OpenRead(),
                    mediaData.ContentType.Value,
                    cancellationToken);
            });

            if (status == MediaStatus.UPLOADED)
            {
                asset.MarkUploaded();
                asset.MarkReady();
            }
            else if (status == MediaStatus.READY)
            {
                asset.MarkUploaded();
                asset.MarkReady();
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return asset;
        });

        return photoAsset;
    }
}