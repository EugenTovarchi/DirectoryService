using AuthService.Core.Abstractions;
using AuthService.Core.Services;
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
            .AddServices()
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
                .AssignableToAny(typeof(ICommandHandler<,>), typeof(ICommandHandler<>), typeof(IQueryHandler<,>)))
            .AsSelfWithInterfaces()
            .WithScopedLifetime());
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<InviteLinkFactory>();
        services.AddScoped<IInviteEmailSender, SmtpInviteEmailSender>();

        return services;
    }
}
