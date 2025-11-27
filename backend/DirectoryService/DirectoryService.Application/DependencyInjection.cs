using DirectoryService.Core.Abstractions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;


namespace DirectoryService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDirectoryServiceApplication(this IServiceCollection services)
    {
        services
            .AddQueries()
            .AddCommands()
            .AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }

    private static IServiceCollection AddCommands(this IServiceCollection services)
    {

        return services.Scan(scan => scan
             .FromAssemblies(typeof(DependencyInjection).Assembly)
             .AddClasses(classes => classes
                 .AssignableToAny(typeof(ICommandHandler<,>), typeof(ICommandHandler<>)))
             .AsSelfWithInterfaces()
             .WithScopedLifetime());
    }
    private static IServiceCollection AddQueries(this IServiceCollection services)
    {

        return services.Scan(scan => scan
              .FromAssemblies(typeof(DependencyInjection).Assembly)
              .AddClasses(classes => classes
                  .AssignableTo(typeof(IQueryHandler<,>)))
              .AsSelfWithInterfaces()
              .WithScopedLifetime());
    }
}
