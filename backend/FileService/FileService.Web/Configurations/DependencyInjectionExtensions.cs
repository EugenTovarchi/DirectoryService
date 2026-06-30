using FileService.VideoProcessing;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SharedService.Framework.Logging;
using SharedService.Framework.Observability;
using SharedService.Framework.Swagger;

namespace FileService.Web.Configurations;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilogLogging(configuration, "FileService");
        services.AddOpenApiSpec("FileService");
        services.AddSharedOpenTelemetry(configuration, fallbackServiceName: "FileService");
        services.ConfigureOpenTelemetryMeterProvider(builder =>
            builder.AddMeter(VideoProcessingTelemetry.METER_NAME));
        services.ConfigureOpenTelemetryTracerProvider(builder =>
            builder.AddSource(VideoProcessingTelemetry.ACTIVITY_SOURCE_NAME));

        return services;
    }
}
