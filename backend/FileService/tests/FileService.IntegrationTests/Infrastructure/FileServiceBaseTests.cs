using Amazon.S3;
using Amazon.S3.Model;
using FileService.Infrastructure.Postgres;
using Microsoft.Extensions.DependencyInjection;

namespace FileService.IntegrationTests.Infrastructure;

public abstract class FileServiceBaseTests : IClassFixture<FileServiceTestWebFactory>, IAsyncLifetime
{
    private readonly Func<Task> _resetDatabase;
    private readonly List<string> _bucketsToCleanup = new();
    private readonly IAmazonS3 _s3Client;

    public const string TEST_FILE_NAME = "test-file.mp4";

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
        // await CleanupBucketsAsync();
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

    /// <summary>
    /// Создает тестовый бакет с уникальным именем и автоматически добавляет его в список для очистки
    /// </summary>
    protected async Task<string> CreateTestBucketAsync(string bucketNamePrefix = "file-test-bucket")
    {
        string bucketName = $"{bucketNamePrefix}".ToLowerInvariant();

        await _s3Client.PutBucketAsync(bucketName);
        _bucketsToCleanup.Add(bucketName);

        return bucketName;
    }

    /// <summary>
    /// Ручное добавление бакета в список очистки (если создали бакет не через CreateTestBucketAsync)
    /// </summary>
    protected void TrackBucketForCleanup(string bucketName)
    {
        if (!_bucketsToCleanup.Contains(bucketName))
        {
            _bucketsToCleanup.Add(bucketName);
        }
    }

    /// <summary>
    /// Очищает все отслеживаемые бакеты
    /// </summary>
    private async Task CleanupBucketsAsync()
    {
        foreach (string bucketName in _bucketsToCleanup)
        {
            await DeleteBucketAsync(bucketName);
        }

        _bucketsToCleanup.Clear();
    }

    /// <summary>
    /// Удаляет бакет со всеми файлами
    /// </summary>
    private async Task DeleteBucketAsync(string bucketName)
    {
        try
        {
            // Проверяем существование бакета
            bool bucketExists = await BucketExistsAsync(bucketName);
            if (!bucketExists) return;

            // Удаляем все объекты (включая версии, если есть)
            await DeleteAllObjectsAsync(bucketName);

            // Удаляем сам бакет
            await _s3Client.DeleteBucketAsync(bucketName);

            Console.WriteLine($"Bucket deleted: {bucketName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete bucket {bucketName}: {ex.Message}");
        }
    }

    private async Task<bool> BucketExistsAsync(string bucketName)
    {
        try
        {
            var response = await _s3Client.ListBucketsAsync();
            return response.Buckets.Any(b => b.BucketName == bucketName);
        }
        catch
        {
            return false;
        }
    }

    private async Task DeleteAllObjectsAsync(string bucketName)
    {
        // Удаляем обычные объекты
        var listRequest = new ListObjectsV2Request { BucketName = bucketName };
        var objects = await _s3Client.ListObjectsV2Async(listRequest);

        if (objects.S3Objects.Any())
        {
            var deleteRequest = new DeleteObjectsRequest
            {
                BucketName = bucketName,
                Objects = objects.S3Objects.Select(obj => new KeyVersion { Key = obj.Key }).ToList()
            };

            await _s3Client.DeleteObjectsAsync(deleteRequest);
        }

        // Удаляем версии объектов (для версионированных бакетов)
        var versionsRequest = new ListVersionsRequest { BucketName = bucketName };
        var versions = await _s3Client.ListVersionsAsync(versionsRequest);

        if (versions.Versions.Any())
        {
            var deleteVersionsRequest = new DeleteObjectsRequest
            {
                BucketName = bucketName,
                Objects = versions.Versions.Select(v =>
                    new KeyVersion { Key = v.Key, VersionId = v.VersionId }).ToList()
            };

            await _s3Client.DeleteObjectsAsync(deleteVersionsRequest);
        }
    }

    /// <summary>
    /// Очищает только файлы в бакете, но оставляет сам бакет
    /// </summary>
    protected async Task ClearBucketAsync(string bucketName)
    {
        if (!_bucketsToCleanup.Contains(bucketName))
        {
            _bucketsToCleanup.Add(bucketName);
        }

        await DeleteAllObjectsAsync(bucketName);
    }
}
