using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SharedService.Core.Abstractions;
using SharedService.Framework.EndpointSettings;

namespace AuthService.Core;

public static class CoreDependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
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
                .AssignableToAny(typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)))
            .AsSelfWithInterfaces()
            .WithScopedLifetime());
    }
}
