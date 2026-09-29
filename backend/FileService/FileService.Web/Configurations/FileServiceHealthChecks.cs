using Amazon.S3;
using Amazon.S3.Model;
using FileService.Infrastructure.S3;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FileService.Web.Configurations;

public static class FileServiceHealthChecks
{
    public const string LIVE_TAG = "live";
    public const string READY_TAG = "ready";

    public static IServiceCollection AddFileServiceHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: new[] { LIVE_TAG })
            .AddCheck<PostgresReadinessHealthCheck>(
                "postgres",
                tags: new[] { READY_TAG })
            .AddCheck<S3ReadinessHealthCheck>(
                "s3",
                tags: new[] { READY_TAG });

        return services;
    }
}

public sealed class PostgresReadinessHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unavailable");
        }
    }
}

public sealed class S3ReadinessHealthCheck(
    IAmazonS3 s3Client,
    IOptions<S3Options> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (string bucketName in options.Value.RequiredBuckets)
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    MaxKeys = 1
                };

                await s3Client.ListObjectsV2Async(request, cancellationToken);
            }

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("S3 storage is unavailable");
        }
    }
}
