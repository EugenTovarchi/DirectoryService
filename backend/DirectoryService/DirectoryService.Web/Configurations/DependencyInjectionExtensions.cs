using SharedService.Framework.Logging;
using SharedService.Framework.Swagger;

namespace DirectoryService.Web.Configurations;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilogLogging(configuration, "DirectoryService");
        services.AddOpenApiSpec("DirectoryService");

        return services;
    }
}
