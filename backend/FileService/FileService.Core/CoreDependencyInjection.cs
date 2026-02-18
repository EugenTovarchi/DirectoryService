using FileService.Core.EndpointSettings;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedService.Core.Abstractions;

namespace FileService.Core;

public static class CoreDependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services
            .AddEndpoints()
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
}
