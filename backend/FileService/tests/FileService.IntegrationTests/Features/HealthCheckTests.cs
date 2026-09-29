using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using FileService.Infrastructure.S3;
using FileService.IntegrationTests.Infrastructure;
using FileService.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FileService.IntegrationTests.Features;

[Collection("FileServiceCollection")]
public sealed class HealthCheckTests : IClassFixture<FileServiceTestWebFactory>, IAsyncLifetime
{
    private readonly FileServiceTestWebFactory _factory;
    private readonly HttpClient _client;
    private readonly IAmazonS3 _s3Client;
    private readonly IReadOnlyList<string> _requiredBuckets;

    public HealthCheckTests(FileServiceTestWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _s3Client = factory.Services.GetRequiredService<IAmazonS3>();
        _requiredBuckets = factory.Services
            .GetRequiredService<IOptions<S3Options>>()
            .Value
            .RequiredBuckets;
    }

    public async Task InitializeAsync()
    {
        foreach (string bucketName in _requiredBuckets)
        {
            try
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
            }
            catch (AmazonS3Exception exception)
                when (exception.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                // Integration-test classes share one MinIO container.
            }
        }
    }

    [Fact]
    public async Task Liveness_WithoutAccessToken_ShouldReturnHealthy()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await _client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_WithAvailableDependencies_ShouldReturnHealthy()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await _client.GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnavailableS3_ShouldFailReadinessButKeepLivenessHealthy()
    {
        // Arrange
        IAmazonS3 unavailableS3 = Substitute.For<IAmazonS3>();
        unavailableS3
            .ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
            .Returns<Task<ListObjectsV2Response>>(_ => throw new AmazonS3Exception("S3 is unavailable"));

        using WebApplicationFactory<Program> unavailableS3Factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAmazonS3>();
                services.AddSingleton(unavailableS3);
            }));
        using HttpClient client = unavailableS3Factory.CreateClient();

        // Act
        HttpResponseMessage readinessResponse = await client.GetAsync("/health/ready");
        HttpResponseMessage livenessResponse = await client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readinessResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, livenessResponse.StatusCode);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
