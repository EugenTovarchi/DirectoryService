using FileService.Core.Abstractions;
using FileService.VideoProcessing.FfmpegProcess;
using FileService.VideoProcessing.Pipeline;
using FileService.VideoProcessing.Pipeline.Options;
using FileService.VideoProcessing.Preview;
using FileService.VideoProcessing.ProcessRunner;
using FileService.VideoProcessing.Quartz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace FileService.VideoProcessing;

public static class VideoProcessingDependencyInjection
{
    public static IServiceCollection AddVideoProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        IConfigurationSection videoOptionsSection = configuration.GetSection(VideoProcessingOptions.SECTION_NAME);

        services.AddSingleton<IValidateOptions<VideoProcessingOptions>, VideoProcessingOptionsValidator>();
        services.AddSingleton<IValidateOptions<PreviewOptions>, PreviewOptionsValidator>();

        services.AddOptions<VideoProcessingOptions>()
            .Bind(videoOptionsSection)
            .ValidateOnStart();

        services.AddOptions<PreviewOptions>()
            .Bind(configuration.GetSection(PreviewOptions.SECTION_NAME))
            .ValidateOnStart();

        VideoProcessingOptions videoOptions = videoOptionsSection.Get<VideoProcessingOptions>() ?? new();

        services.AddScoped<IVideoProcessingService, VideoProcessingService>();
        services.AddScoped<IProcessingPipeline, ProcessingPipeline>();
        services.AddSingleton<IProcessingErrorClassifier, ProcessingErrorClassifier>();
        services.AddSingleton<IVideoProcessingPolicy, ConfiguredVideoProcessingPolicy>();
        services.AddSingleton<VideoProcessingTelemetry>();
        services.AddScoped<IVideoProcessingScheduler, VideoProcessingScheduler>();
        services.AddScoped<IPreviewCalculator, PreviewCalculator>();
        services.AddScoped<IFfmpegProcessRunner, FfmpegProcessRunner>();
        services.AddScoped<IPreviewUploader, PreviewUploader>();
        services.AddScoped<IDataProcessRunner, DataProcessRunner>();

        // Добавляем все Handlers
        services.Scan(scan => scan
            .FromAssemblyOf<IProcessingStepHandler>()
            .AddClasses(classes => classes.AssignableTo<IProcessingStepHandler>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddQuartz(q =>
        {
            q.UseDefaultThreadPool(options => options.MaxConcurrency = videoOptions.MaxConcurrentJobs);

            if (videoOptions.EnableTempCleanupJob)
            {
                var cleanupJobKey = new JobKey(
                    TempDirectoryCleanupJob.JOB_NAME,
                    VideoProcessingScheduler.GROUP_NAME);
                q.AddJob<TempDirectoryCleanupJob>(job => job.WithIdentity(cleanupJobKey));
                q.AddTrigger(trigger => trigger
                    .WithIdentity("VideoProcessingTempCleanupTrigger", VideoProcessingScheduler.GROUP_NAME)
                    .ForJob(cleanupJobKey)
                    .StartNow()
                    .WithSimpleSchedule(schedule => schedule
                        .WithIntervalInMinutes(videoOptions.TempCleanupIntervalMinutes)
                        .RepeatForever()
                        .WithMisfireHandlingInstructionNextWithExistingCount()));
            }

            if (!videoOptions.UsePersistentStore)
            {
                q.UseInMemoryStore();
                return;
            }

            string connectionString = configuration.GetConnectionString("DefaultConnection")
                                      ?? throw new InvalidOperationException(
                                          "ConnectionStrings:DefaultConnection is required for Quartz persistent store");

            q.UsePersistentStore(store =>
            {
                store.PerformSchemaValidation = true;
                store.UseProperties = true;
                store.UsePostgres(postgres =>
                {
                    postgres.ConnectionString = connectionString;
                    postgres.TablePrefix = videoOptions.QuartzTablePrefix;
                });
                store.UseSystemTextJsonSerializer();

                if (videoOptions.UseQuartzClustering)
                {
                    store.UseClustering(clustering =>
                    {
                        clustering.CheckinInterval =
                            TimeSpan.FromSeconds(videoOptions.ClusterCheckinIntervalSeconds);
                        clustering.CheckinMisfireThreshold =
                            TimeSpan.FromSeconds(videoOptions.ClusterCheckinMisfireThresholdSeconds);
                    });
                }
            });
        });

        services.AddQuartzHostedService(q =>
        {
            q.WaitForJobsToComplete = true;
            q.AwaitApplicationStarted = true;
        });
        if (videoOptions.EnableRecoveryService)
            services.AddHostedService<VideoProcessingRecoveryService>();

        return services;
    }
}
