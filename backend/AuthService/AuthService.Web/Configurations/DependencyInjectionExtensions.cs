using AuthService.Core.Options;
using SharedService.Framework.Logging;
using SharedService.Framework.Observability;
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
        configuration.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
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
        services.AddSharedOpenTelemetry(configuration, fallbackServiceName: "AuthService");
        services.AddEmailOptions(configuration);

        return services;
    }

    private static IServiceCollection AddEmailOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SECTION_NAME))
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.SmtpHost),
                "Email:SmtpHost is required when Email:Enabled is true")
            .Validate(
                options => !options.Enabled || options.SmtpPort > 0,
                "Email:SmtpPort must be positive when Email:Enabled is true")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.FromEmail),
                "Email:FromEmail is required when Email:Enabled is true")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.InviteBaseUrl),
                "Email:InviteBaseUrl is required when Email:Enabled is true")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.PasswordResetBaseUrl),
                "Email:PasswordResetBaseUrl is required when Email:Enabled is true")
            .ValidateOnStart();

        return services;
    }
}
