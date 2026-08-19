using System.Diagnostics;
using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using FileService.Core.Abstractions;
using FileService.Domain;
using FileService.Domain.Assets;
using FileService.Domain.MediaProcessing;
using FileService.VideoProcessing;
using FileService.VideoProcessing.Pipeline;
using FileService.VideoProcessing.Pipeline.Options;
using FileService.VideoProcessing.Quartz;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Quartz;
using SharedService.SharedKernel;

namespace FileService.IntegrationTests.Features;

public class VideoProcessingSchedulingTests
{
    [Fact]
    public async Task ScheduleProcessingAsync_ShouldWriteVideoProcessIdToNewJob()
    {
        // Arrange
        var schedulerFactory = Substitute.For<ISchedulerFactory>();
        var scheduler = Substitute.For<IScheduler>();
        schedulerFactory.GetScheduler(Arg.Any<CancellationToken>()).Returns(scheduler);
        scheduler.IsStarted.Returns(true);
        scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(false);
        var sut = new VideoProcessingScheduler(
            schedulerFactory,
            NullLogger<VideoProcessingScheduler>.Instance);
        Guid videoAssetId = Guid.NewGuid();
        Guid videoProcessId = Guid.NewGuid();

        // Act
        UnitResult<Error> result = await sut.ScheduleProcessingAsync(videoAssetId, videoProcessId);

        // Assert
        Assert.True(result.IsSuccess);
        IJobDetail job = scheduler.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault())
            .OfType<IJobDetail>()
            .Single();
        Assert.Equal(videoProcessId.ToString(), job.JobDataMap.GetString("VideoProcessId"));
        Assert.Equal(videoAssetId.ToString(), job.JobDataMap.GetString("VideoAssetId"));
    }

    [Fact]
    public async Task ScheduleProcessingAsync_WhenDurableJobExists_ShouldWriteVideoProcessIdToRecoveryTrigger()
    {
        // Arrange
        var schedulerFactory = Substitute.For<ISchedulerFactory>();
        var scheduler = Substitute.For<IScheduler>();
        schedulerFactory.GetScheduler(Arg.Any<CancellationToken>()).Returns(scheduler);
        scheduler.IsStarted.Returns(true);
        scheduler.CheckExists(Arg.Any<JobKey>(), Arg.Any<CancellationToken>()).Returns(true);
        scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ITrigger>());
        scheduler.GetCurrentlyExecutingJobs(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<IJobExecutionContext>());
        var sut = new VideoProcessingScheduler(
            schedulerFactory,
            NullLogger<VideoProcessingScheduler>.Instance);
        Guid videoAssetId = Guid.NewGuid();
        Guid videoProcessId = Guid.NewGuid();

        // Act
        UnitResult<Error> result = await sut.ScheduleProcessingAsync(videoAssetId, videoProcessId);

        // Assert
        Assert.True(result.IsSuccess);
        ITrigger trigger = scheduler.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault())
            .OfType<ITrigger>()
            .Single();
        Assert.Equal(videoProcessId.ToString(), trigger.JobDataMap.GetString("VideoProcessId"));
    }

    [Fact]
    public async Task Execute_WhenAttemptFailsTransiently_ShouldKeepVideoProcessIdInRetryTrigger()
    {
        // Arrange
        Guid videoAssetId = Guid.NewGuid();
        VideoAsset videoAsset = VideoAsset.CreateForUpload(
            videoAssetId,
            MediaData.Create(
                FileName.Create("video.mp4").Value,
                ContentType.Create("video/mp4").Value,
                1024,
                1).Value,
            Guid.NewGuid(),
            "Department").Value;
        Assert.True(videoAsset.MarkUploaded().IsSuccess);
        VideoProcess process = VideoProcess.Create(videoAssetId, videoAsset.RawKey!).Value;
        Guid videoProcessId = process.Id;
        var processRepository = Substitute.For<IVideoProcessesRepository>();
        processRepository.GetBy(
                Arg.Any<Expression<Func<VideoProcess, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<VideoProcess, Error>>(process));
        var mediaRepository = Substitute.For<IMediaAssetsRepository>();
        mediaRepository.GetBy(
                Arg.Any<Expression<Func<MediaAsset, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<MediaAsset, Error>>(videoAsset));
        var transactionManager = Substitute.For<ITransactionManager>();
        transactionManager.SaveChangeAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(UnitResult.Success<Error>()));
        var processingService = Substitute.For<IVideoProcessingService>();
        bool startStepSucceeded = false;
        bool failProcessSucceeded = false;
        processingService.ProcessVideoAsync(videoAssetId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                VideoProcessStep step = process.Steps[0];
                startStepSucceeded = process.StartStep(step.Order, step.Name).IsSuccess;
                failProcessSucceeded = process.Fail("Transient FFmpeg failure", isCritical: false).IsSuccess;
                return Task.FromResult<UnitResult<Error>>(
                    Error.Failure("ffmpeg.transient", "Transient FFmpeg failure"));
            });
        var processingPolicy = Substitute.For<IVideoProcessingPolicy>();
        processingPolicy.GetRetryDelay(Arg.Any<int>()).Returns(TimeSpan.Zero);
        var sut = new VideoProcessingJob(
            processingService,
            mediaRepository,
            NullLogger<VideoProcessingJob>.Instance,
            transactionManager,
            processRepository,
            processingPolicy,
            new VideoProcessingTelemetry());
        IJobDetail job = JobBuilder.Create<VideoProcessingJob>()
            .WithIdentity("retry-source")
            .UsingJobData("VideoAssetId", videoAssetId.ToString())
            .UsingJobData("VideoProcessId", videoProcessId.ToString())
            .UsingJobData("AttemptNumber", "1")
            .Build();
        var quartzScheduler = Substitute.For<IScheduler>();
        var context = Substitute.For<IJobExecutionContext>();
        context.JobDetail.Returns(job);
        context.MergedJobDataMap.Returns(job.JobDataMap);
        context.Scheduler.Returns(quartzScheduler);

        // Act
        await sut.Execute(context);

        // Assert
        ITrigger retryTrigger = quartzScheduler.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault())
            .OfType<ITrigger>()
            .Single();
        Assert.Equal(videoProcessId, process.Id);
        Assert.True(startStepSucceeded);
        Assert.True(failProcessSucceeded);
        Assert.Equal(videoProcessId.ToString(), retryTrigger.JobDataMap.GetString("VideoProcessId"));
        Assert.Equal("2", retryTrigger.JobDataMap.GetString("AttemptNumber"));
    }

    [Fact]
    public async Task Execute_WhenLegacyJobHasNoVideoProcessId_ShouldResolveExistingProcessAndCreateNewRootTrace()
    {
        // Arrange
        Guid videoAssetId = Guid.NewGuid();
        VideoProcess process = VideoProcess.Create(
            videoAssetId,
            StorageKey.Create($"{videoAssetId}.mp4", "raw", "file-service-videos").Value).Value;
        var processRepository = Substitute.For<IVideoProcessesRepository>();
        processRepository.GetBy(
                Arg.Any<Expression<Func<VideoProcess, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<VideoProcess, Error>>(process));
        var mediaRepository = Substitute.For<IMediaAssetsRepository>();
        mediaRepository.GetBy(
                Arg.Any<Expression<Func<MediaAsset, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<MediaAsset, Error>>(
                Error.NotFound("video.not.found", "Video not found")));
        var transactionManager = Substitute.For<ITransactionManager>();
        transactionManager.SaveChangeAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(UnitResult.Success<Error>()));
        var logger = new ScopeCapturingLogger<VideoProcessingJob>();
        var sut = new VideoProcessingJob(
            Substitute.For<IVideoProcessingService>(),
            mediaRepository,
            logger,
            transactionManager,
            processRepository,
            Substitute.For<IVideoProcessingPolicy>(),
            new VideoProcessingTelemetry());
        var traceIds = new List<ActivityTraceId>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == VideoProcessingTelemetry.ACTIVITY_SOURCE_NAME,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => traceIds.Add(activity.TraceId),
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        await sut.Execute(CreateLegacyContext(videoAssetId, 1));
        await sut.Execute(CreateLegacyContext(videoAssetId, 2));

        // Assert
        Assert.Equal(2, logger.Scopes.Count);
        Assert.All(logger.Scopes, scope => Assert.Equal(process.Id, scope["VideoProcessId"]));
        Assert.Equal(2, traceIds.Count);
        Assert.Equal(2, traceIds.Distinct().Count());
    }

    [Fact]
    public async Task RecoveryService_ShouldSchedulePersistedVideoProcessId()
    {
        // Arrange
        Guid videoAssetId = Guid.NewGuid();
        Guid videoProcessId = Guid.NewGuid();
        var repository = Substitute.For<IVideoProcessesRepository>();
        repository.GetRecoverableVideoProcessesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IReadOnlyList<RecoverableVideoProcess>, Error>>(
                new[] { new RecoverableVideoProcess(videoProcessId, videoAssetId, null) }));
        var scheduler = Substitute.For<IVideoProcessingScheduler>();
        var scheduled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.ScheduleProcessingAsync(
                videoAssetId,
                videoProcessId,
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                scheduled.TrySetResult();
                return Task.FromResult(UnitResult.Success<Error>());
            });
        await using ServiceProvider services = new ServiceCollection()
            .AddSingleton(repository)
            .AddSingleton(scheduler)
            .BuildServiceProvider();
        var service = new VideoProcessingRecoveryService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new VideoProcessingOptions { RecoveryScanIntervalSeconds = 60 }),
            NullLogger<VideoProcessingRecoveryService>.Instance);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await service.StartAsync(cancellation.Token);
        await scheduled.Task.WaitAsync(cancellation.Token);
        await service.StopAsync(CancellationToken.None);

        // Assert
        await scheduler.Received(1).ScheduleProcessingAsync(
            videoAssetId,
            videoProcessId,
            null,
            Arg.Any<CancellationToken>());
    }

    private static IJobExecutionContext CreateLegacyContext(Guid videoAssetId, int attemptNumber)
    {
        IJobDetail job = JobBuilder.Create<VideoProcessingJob>()
            .WithIdentity($"legacy-{attemptNumber}")
            .UsingJobData("VideoAssetId", videoAssetId.ToString())
            .UsingJobData("AttemptNumber", attemptNumber.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Build();
        var context = Substitute.For<IJobExecutionContext>();
        context.JobDetail.Returns(job);
        context.MergedJobDataMap.Returns(job.JobDataMap);
        context.Scheduler.Returns(Substitute.For<IScheduler>());
        return context;
    }

    private sealed class ScopeCapturingLogger<T> : ILogger<T>
    {
        public List<IReadOnlyDictionary<string, object>> Scopes { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            if (state is IEnumerable<KeyValuePair<string, object>> properties)
                Scopes.Add(properties.ToDictionary());
            return new NullScope();
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
