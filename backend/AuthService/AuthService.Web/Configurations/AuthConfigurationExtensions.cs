using System.Text;
using AuthService.Core.Authorization;
using AuthService.Core.Options;
using AuthService.Domain.Identity;
using AuthService.Web.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Web.Configurations;

public static class AuthConfigurationExtensions
{
    public static IServiceCollection AddAuthServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddJwtOptions(configuration);
        services.AddServiceClientOptions(configuration);

        var jwtOptions = configuration
            .GetSection(JwtOptions.SECTION_NAME)
            .Get<JwtOptions>() ?? new JwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
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

    private static IServiceCollection AddJwtOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SECTION_NAME))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SigningKey) &&
                    options.SigningKey.Length >= JwtOptions.MIN_SIGNING_KEY_LENGTH,
                $"Jwt:SigningKey must be at least {JwtOptions.MIN_SIGNING_KEY_LENGTH} characters")
            .Validate(options => options.AccessTokenLifetimeMinutes > 0, "Jwt:AccessTokenLifetimeMinutes must be positive")
            .Validate(options => options.RefreshTokenLifetimeDays > 0, "Jwt:RefreshTokenLifetimeDays must be positive")
            .ValidateOnStart();

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
