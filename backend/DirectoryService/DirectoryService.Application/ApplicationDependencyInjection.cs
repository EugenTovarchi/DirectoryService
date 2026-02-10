using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SharedService.Core.Abstractions;

namespace DirectoryService.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddDirectoryServiceApplication(this IServiceCollection services)
    {
        services
            .AddQueries()
            .AddCommands()
            .AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);

        return services;
    }

    private static IServiceCollection AddCommands(this IServiceCollection services)
    {
        return services.Scan(scan => scan
             .FromAssemblies(typeof(ApplicationDependencyInjection).Assembly)
             .AddClasses(classes => classes
                 .AssignableToAny(typeof(ICommandHandler<,>), typeof(ICommandHandler<>)))
             .AsSelfWithInterfaces()
             .WithScopedLifetime());
    }

    private static IServiceCollection AddQueries(this IServiceCollection services)
    {
        return services.Scan(scan => scan
              .FromAssemblies(typeof(ApplicationDependencyInjection).Assembly)
              .AddClasses(classes => classes
                  .AssignableTo(typeof(IQueryHandler<,>)))
              .AsSelfWithInterfaces()
              .WithScopedLifetime());
    }
}
