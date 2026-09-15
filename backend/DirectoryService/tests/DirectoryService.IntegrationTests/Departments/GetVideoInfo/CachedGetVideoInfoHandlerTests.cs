using CSharpFunctionalExtensions;
using DirectoryService.Application.Cache;
using DirectoryService.Application.Queries.Departments.GetVideoInfo;
using DirectoryService.Contracts.Responses;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharedService.SharedKernel;

namespace DirectoryService.IntegrationTests.Departments.GetVideoInfo;

public class CachedGetVideoInfoHandlerTests
{
    [Fact]
    public async Task Handle_ready_video_with_complete_metadata_should_be_cached()
    {
        // Arrange
        await using ServiceProvider services = CreateCacheServices();
        var inner = Substitute.For<IGetVideoInfoHandler>();
        var query = new GetVideoInfoQuery(Guid.NewGuid());
        GetVideoInfoResponse response = CreateResponse("ready");

        inner.Handle(query, Arg.Any<CancellationToken>())
            .Returns(response);

        var sut = CreateSut(inner, services);

        // Act
        var first = await sut.Handle(query, CancellationToken.None);
        var second = await sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(response, first.Value);
        Assert.Equal(response, second.Value);
        await inner.Received(1).Handle(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_failure_should_not_be_cached()
    {
        // Arrange
        await using ServiceProvider services = CreateCacheServices();
        var inner = Substitute.For<IGetVideoInfoHandler>();
        var query = new GetVideoInfoQuery(Guid.NewGuid());
        Failure failure = Errors.General.NotFoundEntity("video").ToFailure();

        inner.Handle(query, Arg.Any<CancellationToken>())
            .Returns(failure);

        var sut = CreateSut(inner, services);

        // Act
        var first = await sut.Handle(query, CancellationToken.None);
        var second = await sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(first.IsFailure);
        Assert.True(second.IsFailure);
        Assert.Equal(failure, first.Error);
        Assert.Equal(failure, second.Error);
        await inner.Received(2).Handle(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_processing_video_should_not_be_cached()
    {
        // Arrange
        await using ServiceProvider services = CreateCacheServices();
        var inner = Substitute.For<IGetVideoInfoHandler>();
        var query = new GetVideoInfoQuery(Guid.NewGuid());
        GetVideoInfoResponse response = CreateResponse("processing");

        inner.Handle(query, Arg.Any<CancellationToken>())
            .Returns(response);

        var sut = CreateSut(inner, services);

        // Act
        await sut.Handle(query, CancellationToken.None);
        await sut.Handle(query, CancellationToken.None);

        // Assert
        await inner.Received(2).Handle(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ready_video_with_incomplete_metadata_should_not_be_cached()
    {
        // Arrange
        await using ServiceProvider services = CreateCacheServices();
        var inner = Substitute.For<IGetVideoInfoHandler>();
        var query = new GetVideoInfoQuery(Guid.NewGuid());
        GetVideoInfoResponse response = CreateResponse("ready", hasCompleteMetadata: false);

        inner.Handle(query, Arg.Any<CancellationToken>())
            .Returns(response);

        var sut = CreateSut(inner, services);

        // Act
        await sut.Handle(query, CancellationToken.None);
        await sut.Handle(query, CancellationToken.None);

        // Assert
        await inner.Received(2).Handle(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_after_departments_tag_invalidation_should_reload_video_info()
    {
        // Arrange
        await using ServiceProvider services = CreateCacheServices();
        var inner = Substitute.For<IGetVideoInfoHandler>();
        var query = new GetVideoInfoQuery(Guid.NewGuid());
        GetVideoInfoResponse response = CreateResponse("ready");
        HybridCache cache = services.GetRequiredService<HybridCache>();

        inner.Handle(query, Arg.Any<CancellationToken>())
            .Returns(response);

        var sut = CreateSut(inner, services);

        // Act
        await sut.Handle(query, CancellationToken.None);
        await sut.Handle(query, CancellationToken.None);
        await cache.RemoveByTagAsync("departments", CancellationToken.None);
        await sut.Handle(query, CancellationToken.None);

        // Assert
        await inner.Received(2).Handle(query, Arg.Any<CancellationToken>());
    }

    private static CachedGetVideoInfoHandler CreateSut(
        IGetVideoInfoHandler inner,
        IServiceProvider services)
    {
        var options = Options.Create(new CacheOptions
        {
            DepartmentsCacheDurationMinutes = 5,
            DefaultLocalCacheDurationMinutes = 1
        });

        return new CachedGetVideoInfoHandler(
            inner,
            services.GetRequiredService<HybridCache>(),
            options);
    }

    private static ServiceProvider CreateCacheServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddHybridCache();

        return services.BuildServiceProvider();
    }

    private static GetVideoInfoResponse CreateResponse(
        string status,
        bool hasCompleteMetadata = true)
    {
        return new GetVideoInfoResponse(
            Guid.NewGuid(),
            "video.mp4",
            "video/mp4",
            status,
            DateTime.UtcNow,
            DateTime.UtcNow,
            1024,
            hasCompleteMetadata ? TimeSpan.FromSeconds(30) : null,
            1920,
            1080,
            true);
    }
}
