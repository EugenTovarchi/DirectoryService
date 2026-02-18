using SharedService.Framework.Logging;
using SharedService.Framework.Swagger;

namespace FileService.Web.Configurations;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilogLogging(configuration, "FileService");
        services.AddOpenApiSpec("FileService");

        return services;
    }
}
