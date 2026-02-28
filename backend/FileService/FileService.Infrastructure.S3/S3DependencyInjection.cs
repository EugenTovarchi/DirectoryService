using Amazon.S3;
using FileService.Core;
using FileService.Core.FilesStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;

namespace FileService.Infrastructure.S3;

public static class S3DependencyInjection
{
    public static IServiceCollection AddS3(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<S3Options>(configuration.GetSection(nameof(S3Options)));

        services.AddScoped<IFileStorageProvider, FileStorageProvider>();

        services.AddSingleton<IAmazonS3>(sp =>
        {
            S3Options s3Options = sp.GetRequiredService<IOptions<S3Options>>().Value;

            var config = new AmazonS3Config { ServiceURL = s3Options.Endpoint, UseHttp = true, ForcePathStyle = true, };

            return new AmazonS3Client(s3Options.AccessKey, s3Options.SecretKey, config);
        });

        // services.AddHostedService<S3BucketInitializationService>();
        services.AddTransient<IChunkSizeCalculator, ChunkSizeCalculator>();

        return services;
    }
}