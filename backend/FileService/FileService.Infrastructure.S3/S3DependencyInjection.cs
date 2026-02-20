using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FileService.Infrastructure.S3;

public static class S3DependencyInjection
{
    public static IServiceCollection AddS3(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<S3Options>(configuration.GetSection(nameof(S3Options)));

        services.AddSingleton<IAmazonS3>(sp =>
        {
            S3Options s3Options = sp.GetRequiredService<IOptions<S3Options>>().Value;

            var config = new AmazonS3Config
            {
                ServiceURL = s3Options.Endpoint, UseHttp = !s3Options.WithSsl, ForcePathStyle = true
            };

            return new AmazonS3Client(s3Options.AccessKey,  s3Options.SecretKey, config);
        });

        // services.AddHostedService<S3BucketInitalizationService>();
        return services;
    }
}
