using AuthService.Core.Authorization;
using AuthService.Core.Options;
using AuthService.Domain.Identity;
using AuthService.Web.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;

namespace AuthService.Web.Configurations;

public static class AuthConfigurationExtensions
{
    public static IServiceCollection AddAuthServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddServiceClientOptions(configuration);

        services
            .AddAuthentication(options =>
            {
                // Защищённые API принимают только access tokens локального OpenIddict server.
                options.DefaultAuthenticateScheme =
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme =
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            })
            .AddCookie(OidcBrowserAuthenticationDefaults.SCHEME, options =>
            {
                // Cookie нужна только для интерактивных login/authorize pages.
                // Она не становится default scheme и не заменяет Bearer JWT в API.
                options.Cookie.Name = OidcBrowserAuthenticationDefaults.COOKIE_NAME;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.LoginPath = "/connect/login";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = false;
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthPolicies.USERS_MANAGE,
                policy => policy.RequireClaim(AuthClaimTypes.PERMISSION, AuthPermissions.USERS_MANAGE));
        });

        return services;
    }

    private static IServiceCollection AddServiceClientOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<ServiceClientOptions>, ServiceClientOptionsValidator>();
        services
            .AddOptions<ServiceClientOptions>()
            .Bind(configuration.GetSection(ServiceClientOptions.SECTION_NAME))
            .ValidateOnStart();

        return services;
    }
}
