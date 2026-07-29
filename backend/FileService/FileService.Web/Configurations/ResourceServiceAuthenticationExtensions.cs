using System.Text;
using FileService.Core.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FileService.Web.Configurations;

public static class ResourceServiceAuthenticationExtensions
{
    private const string JWT_SECTION_NAME = "Jwt";
    private const string PERMISSION_CLAIM = "permission";
    private const string SERVICE_PERMISSION_CLAIM = "service_permission";
    private const int MIN_SIGNING_KEY_LENGTH = 32;

    // Проверка конфигурации при старте приложения(настройка самого FS, а не каждого JWT).
    public static IServiceCollection AddResourceServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<JwtValidationOptions>()
            .Bind(configuration.GetSection(JWT_SECTION_NAME))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required")
            .Validate(
                options => HasValidSigningSource(options),
                $"Jwt requires MetadataAddress or SigningKey with at least {MIN_SIGNING_KEY_LENGTH} characters")
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtValidationOptions>>((options, jwtOptions) =>
            {
                options.MapInboundClaims = false;
                ConfigureJwtBearer(options, jwtOptions.Value);
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                FileAuthorizationPolicies.FILES_READ,
                policy => policy.RequireClaim(PERMISSION_CLAIM, FileAuthorizationPolicies.FILES_READ));
            options.AddPolicy(
                FileAuthorizationPolicies.FILES_UPLOAD,
                policy => policy.RequireClaim(PERMISSION_CLAIM, FileAuthorizationPolicies.FILES_UPLOAD));
            options.AddPolicy(
                FileAuthorizationPolicies.FILES_DELETE,
                policy => policy.RequireClaim(PERMISSION_CLAIM, FileAuthorizationPolicies.FILES_DELETE));
            options.AddPolicy(
                FileAuthorizationPolicies.FILE_SERVICE_INTERNAL,
                policy => policy.RequireClaim(
                    SERVICE_PERMISSION_CLAIM,
                    FileAuthorizationPolicies.FILE_SERVICE_INTERNAL));
        });

        return services;
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions bearerOptions,
        JwtValidationOptions jwtOptions)
    {
        bearerOptions.TokenValidationParameters = CreateTokenValidationParameters(jwtOptions);

        if (string.IsNullOrWhiteSpace(jwtOptions.MetadataAddress))
        {
            bearerOptions.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
            return;
        }

        // JwtBearer загружает discovery/JWKS и автоматически обновляет public signing keys.
        bearerOptions.MetadataAddress = jwtOptions.MetadataAddress;
        bearerOptions.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
    }

    private static TokenValidationParameters CreateTokenValidationParameters(JwtValidationOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    private static bool HasValidSigningSource(JwtValidationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.MetadataAddress))
        {
            return Uri.TryCreate(
                options.MetadataAddress,
                UriKind.Absolute,
                out _);
        }

        return !string.IsNullOrWhiteSpace(options.SigningKey) &&
            options.SigningKey.Length >= MIN_SIGNING_KEY_LENGTH;
    }

    private sealed class JwtValidationOptions
    {
        public string Issuer { get; init; } = string.Empty;
        public string Audience { get; init; } = string.Empty;
        public string SigningKey { get; init; } = string.Empty;
        public string MetadataAddress { get; init; } = string.Empty;
        public bool RequireHttpsMetadata { get; init; } = true;
    }
}
