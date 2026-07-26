using System.Threading.RateLimiting;
using AuthService.Core.Options;
using AuthService.Core.RateLimiting;
using AuthService.Web.Swagger;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
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
        services.AddSwaggerGen(options => options.SchemaFilter<LoginRequestSchemaFilter>());
        services.AddSharedOpenTelemetry(configuration, fallbackServiceName: "AuthService");
        services.AddEmailOptions(configuration);
        services.AddPublicAuthRateLimiting(configuration);

        return services;
    }

    private static IServiceCollection AddPublicAuthRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Endpoint policies читают IOptionsMonitor во время запроса, чтобы test overrides (переопределения в тестах)
        // и config reload (перезагрузка конфигурации) влияли на новые окна.
        services.AddSingleton<IValidateOptions<PublicAuthRateLimitOptions>, PublicAuthRateLimitOptionsValidator>();
        services
            .AddOptions<PublicAuthRateLimitOptions>()
            .Bind(configuration.GetSection(PublicAuthRateLimitOptions.SECTION_NAME))
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            AddFixedWindowPolicy(
                options,
                PublicAuthRateLimitPolicies.LOGIN,
                rateLimitOptions => rateLimitOptions.LoginPermitLimit);
            AddFixedWindowPolicy(
                options,
                PublicAuthRateLimitPolicies.REFRESH,
                rateLimitOptions => rateLimitOptions.RefreshPermitLimit);
            AddFixedWindowPolicy(
                options,
                PublicAuthRateLimitPolicies.PASSWORD_RESET,
                rateLimitOptions => rateLimitOptions.PasswordResetPermitLimit);
            AddFixedWindowPolicy(
                options,
                PublicAuthRateLimitPolicies.INVITE_RESEND,
                rateLimitOptions => rateLimitOptions.InviteResendPermitLimit);
        });

        return services;
    }

    private static void AddFixedWindowPolicy(
        RateLimiterOptions options,
        string policyName,
        Func<PublicAuthRateLimitOptions, int> getPermitLimit)
    {
        options.AddPolicy(policyName, httpContext =>
        {
            PublicAuthRateLimitOptions rateLimitOptions = httpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<PublicAuthRateLimitOptions>>()
                .CurrentValue;
            int permitLimit = rateLimitOptions.Enabled
                ? getPermitLimit(rateLimitOptions)
                : int.MaxValue;

            // Часть public auth flows (сценариев авторизации) анонимная, поэтому для MVP используем IP
            // как стабильный partition key (ключ группировки запросов).
            return
            RateLimitPartition.GetFixedWindowLimiter(
                GetClientPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = permitLimit,
                    QueueLimit = 0,
                    Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                });
        });
    }

    private static string GetClientPartitionKey(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static IServiceCollection AddEmailOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SECTION_NAME))
            .ValidateOnStart();

        return services;
    }
}
