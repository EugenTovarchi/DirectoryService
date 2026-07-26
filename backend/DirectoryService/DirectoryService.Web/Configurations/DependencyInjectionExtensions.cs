using Microsoft.OpenApi;
using SharedService.Framework.Logging;
using SharedService.Framework.Observability;
using SharedService.Framework.Swagger;

namespace DirectoryService.Web.Configurations;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilogLogging(configuration, "DirectoryService");
        services.AddOpenApiSpec("DirectoryService");
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "AuthService access token"
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });
        services.AddSharedOpenTelemetry(configuration, fallbackServiceName: "DirectoryService");

        return services;
    }
}
