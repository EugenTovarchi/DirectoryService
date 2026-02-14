using DirectoryService.Application.Cache;
using FluentValidation;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedService.Core.Abstractions;

namespace DirectoryService.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddDirectoryServiceApplication(this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddQueries()
            .AddCommands()
            .AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly)
            .AddCache(configuration);

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

    public static IServiceCollection AddCache(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.CACHE));

        var cacheOptions = configuration.GetSection(CacheOptions.CACHE)
            .Get<CacheOptions>() ?? throw new InvalidOperationException("Missing cache options");

        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(cacheOptions.DefaultLocalCacheDurationMinutes),
                Expiration = TimeSpan.FromMinutes(cacheOptions.DefaultCacheDurationMinutes)
            };
        });

        string? redisConnectionString = configuration.GetConnectionString("Redis");

        services.AddStackExchangeRedisCache(setup =>
        {
            setup.Configuration = redisConnectionString;
        });

        return services;
    }
}
