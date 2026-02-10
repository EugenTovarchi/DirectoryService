using FluentValidation;
using Microsoft.Extensions.Caching.Hybrid;
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

    public static IServiceCollection AddCash(this IServiceCollection services)
    {
         services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(5),
                Expiration = TimeSpan.FromMinutes(30)
            };
        });

         services.AddStackExchangeRedisCache(setup =>
        {
            setup.Configuration = "localhost:6379";
        });

         return services;
    }
}
