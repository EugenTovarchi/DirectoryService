using Microsoft.OpenApi;
using Serilog;
using Serilog.Exceptions;

namespace DirectoryService.Web.Configurations;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddConfiguration (this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilogLogging(configuration);
        services.AddOpenApiSpec();

        return services;
    }

    public static IServiceCollection AddOpenApiSpec(this IServiceCollection services)
    {
        services.AddOpenApi();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "DeviceService",
                Version = "v1",
                Contact = new OpenApiContact
                {
                    Name = "Yudjine"
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddSerilogLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((services, lc) => lc 
        .ReadFrom.Configuration(configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithExceptionDetails()
        .Enrich.WithProperty("ServiceName", "DirectoryService"));

        return services;
    }
}
