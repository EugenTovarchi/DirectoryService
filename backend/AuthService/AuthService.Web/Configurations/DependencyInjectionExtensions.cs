using SharedService.Framework.Logging;
using SharedService.Framework.Swagger;

namespace AuthService.Web.Configurations;

public static class DependencyInjectionExtensions
{
    public static IConfigurationBuilder AddAuthServiceConfiguration<TProgram>(
        this IConfigurationBuilder configuration,
        string environment,
        IWebHostEnvironment webHostEnvironment)
        where TProgram : class
    {
        configuration.AddJsonFile($"appsettings.{environment}.json", true, true)
            .AddEnvironmentVariables();

        if (webHostEnvironment.IsDevelopment())
        {
            configuration.AddUserSecrets<TProgram>(optional: true);
        }

        return configuration;
    }

    public static IServiceCollection AddConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSerilogLogging(configuration, "AuthService");
        services.AddOpenApiSpec("AuthService");

        return services;
    }
}
