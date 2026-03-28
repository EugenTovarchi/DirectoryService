using FileService.Core.Abstractions;
using FileService.VideoProcessing.Pipeline;
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

        services.AddScoped<VideoProcessingService>();
        services.AddScoped<IProcessingPipeline, ProcessingPipeline>();
        services.AddScoped<IVideoProcessingScheduler, VideoProcessingScheduler>();

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