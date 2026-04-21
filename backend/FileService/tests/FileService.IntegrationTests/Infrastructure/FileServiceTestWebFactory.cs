using System.Data.Common;
using Amazon.S3;
using FileService.Core;
using FileService.Core.Abstractions;
using FileService.Core.FilesStorage;
using FileService.Infrastructure.Postgres;
using FileService.Infrastructure.Postgres.Repositories;
using FileService.Infrastructure.S3;
using FileService.IntegrationTests.Mocks;
using FileService.VideoProcessing.FfmpegProcess;
using FileService.VideoProcessing.ProcessRunner;
using FileService.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Respawn;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;

namespace FileService.IntegrationTests.Infrastructure;

public class FileServiceTestWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("file-service")
        .WithUsername("postgresUser")
        .WithPassword("postgresPassword")
        .Build();

    private readonly MinioContainer _minioContainer = new MinioBuilder()
        .WithImage("minio/minio")
        .WithUsername("minioadmin")
        .WithPassword("minioadmin")
        .Build();

    private DbConnection _dbConnection = null!;
    private Respawner _respawner = null!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _minioContainer.StartAsync();

        await CreateDatabaseDirectlyAsync();
        _dbConnection = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await _dbConnection.OpenAsync();

        await InitializeRespawnerAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["S3Options:AccessKey"] = "minioadmin",
                ["S3Options:SecretKey"] = "minioadmin",
                ["S3Options:WithSsl"] = "false",
                ["S3Options:ForcePathStyle"] = "true",
                ["ConnectionStrings:RabbitMq"] = "amqp://localhost:5672",
                ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                ["VideoProcessingOptions:FfmpegPath"] = @"D:\Projects\DirectoryService\ffmpeg\bin\ffmpeg.exe",
                ["VideoProcessingOptions:FfprobePath"] = @"D:\Projects\DirectoryService\ffmpeg\bin\ffprobe.exe",
                ["VideoProcessingOptions:UseHardwareAcceleration"] = "false",
                ["VideoProcessingOptions:VideoEncoder"] = "libx264",
                ["VideoProcessingOptions:VideoPreset"] = "medium"
            };

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<FileServiceDbContext>();
            services.RemoveAll<IFileReadDbContext>();
            services.RemoveAll<IFileStorageProvider>();
            services.RemoveAll<IDataProcessRunner>();
            services.RemoveAll<IAmazonS3>();

            var bucketServiceDescriptor = services.FirstOrDefault(d =>
                d.ImplementationType == typeof(S3BucketInitializationService) ||
                d.ImplementationType?.Name == "S3BucketInitializationService");

            if (bucketServiceDescriptor != null)
            {
                services.Remove(bucketServiceDescriptor);
            }

            services.AddDbContext<FileServiceDbContext>(_ =>
                FileServiceDbContext.Create(_dbContainer.GetConnectionString()));

            services.AddScoped<IFileReadDbContext>(sp =>
                sp.GetRequiredService<FileServiceDbContext>());

            services.AddSingleton<IAmazonS3>(_ =>
            {
                ushort minioPort = _minioContainer.GetMappedPublicPort(9000);

                var config = new AmazonS3Config
                {
                    ServiceURL = $"http://{_minioContainer.Hostname}:{minioPort}",
                    UseHttp = true,
                    ForcePathStyle = true
                };

                return new AmazonS3Client("minioadmin", "minioadmin", config);
            });

            services.AddScoped<IMediaAssetsRepository, MediaAssetsRepository>();
            services.AddScoped<IFileStorageProvider, FileStorageProvider>();
            services.AddTransient<IChunkSizeCalculator, ChunkSizeCalculator>();
            services.AddScoped<IDataProcessRunner, DataDataProcessRunner>();

            services.RemoveAll<IFfmpegProcessRunner>();
            services.AddSingleton<IFfmpegProcessRunner, FakeFfmpegProcessRunner>();
        });
        base.ConfigureWebHost(builder);
    }

    private async Task CreateDatabaseDirectlyAsync()
    {
        await using var dbContext = FileServiceDbContext.Create(_dbContainer.GetConnectionString());

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        await dbContext.DisposeAsync();
    }

    private async Task InitializeRespawnerAsync()
    {
        _respawner = await Respawner.CreateAsync(_dbConnection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = ["__EFMigrationsHistory"]
            });
    }

    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_dbConnection);
    }

    public new async Task DisposeAsync()
    {
        await Task.Delay(3000);
        if (_dbConnection != null)
        {
            await _dbConnection.CloseAsync();
            await _dbConnection.DisposeAsync();
        }

        await _dbContainer.StopAsync();
        await _minioContainer.StopAsync();

        await _dbContainer.DisposeAsync();
        await _minioContainer.DisposeAsync();
    }
}