using FileService.Core.Abstractions;
using FileService.VideoProcessing.FfmpegProcess;
using FileService.VideoProcessing.Pipeline;
using FileService.VideoProcessing.Pipeline.Options;
using FileService.VideoProcessing.Preview;
using FileService.VideoProcessing.Quartz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace FileService.VideoProcessing;

public static class VideoProcessingDependencyInjection
{
    public static IServiceCollection AddVideoProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<VideoProcessingOptions>(
            configuration.GetSection(nameof(VideoProcessingOptions)));

        services.Configure<PreviewOptions>(
            configuration.GetSection(nameof(PreviewOptions.SECTION_NAME)));

        services.AddScoped<IVideoProcessingService, VideoProcessingService>();
        services.AddScoped<IProcessingPipeline, ProcessingPipeline>();
        services.AddScoped<IVideoProcessingScheduler, VideoProcessingScheduler>();
        services.AddScoped<IPreviewCalculator, PreviewCalculator>();
        services.AddScoped<IFfmpegProcessRunner, FfmpegProcessRunner>();
        services.AddScoped<IPreviewUploader, PreviewUploader>();

        // Добавляем все Handlers
        services.Scan(scan => scan
            .FromAssemblyOf<IProcessingStepHandler>()
            .AddClasses(classes => classes.AssignableTo<IProcessingStepHandler>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddQuartz(q =>
        {
            q.UseInMemoryStore();

            q.AddJob<VideoProcessingJob>(opts => opts
                .WithIdentity("VideoProcessingJobTemplate", "VideoProcessingGroup")
                .StoreDurably());
        });

        services.AddQuartzHostedService(q =>
        {
            q.WaitForJobsToComplete = true;
            q.AwaitApplicationStarted = true;
        });

        return services;
    }
}