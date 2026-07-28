using FileService.Core.FilesStorage;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedService.Framework.EndpointSettings;

namespace FileService.Core;

public static class CoreDependencyInjection
{
    public static IServiceCollection AddCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<MultipartUploadOptions>, MultipartUploadOptionsValidator>();
        services.AddOptions<MultipartUploadOptions>()
            .Bind(configuration.GetSection(MultipartUploadOptions.SECTION_NAME))
            .ValidateOnStart();

        services
            .AddEndpoints()
            .AddHandlers()
            .AddValidatorsFromAssembly(typeof(CoreDependencyInjection).Assembly);

        return services;
    }

    private static IServiceCollection AddEndpoints(this IServiceCollection services)
    {
        return services.Scan(scan => scan
             .FromAssemblies(typeof(CoreDependencyInjection).Assembly)
             .AddClasses(classes => classes
                 .AssignableToAny(typeof(IEndpoint)))
             .AsSelfWithInterfaces()
             .WithScopedLifetime());
    }

    private static IServiceCollection AddHandlers(this IServiceCollection services)
    {
        return services.Scan(scan => scan
            .FromAssemblies(typeof(CoreDependencyInjection).Assembly)
            .AddClasses(classes => classes
                .Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal)))
            .AsSelf()
            .WithScopedLifetime());
    }
}