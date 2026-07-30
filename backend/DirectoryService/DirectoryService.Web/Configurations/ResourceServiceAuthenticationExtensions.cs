using System.Text;
using DirectoryService.Web.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DirectoryService.Web.Configurations;

public static class ResourceServiceAuthenticationExtensions
{
    private const string JWT_SECTION_NAME = "Jwt";
    private const string PERMISSION_CLAIM = "permission";
    private const int MIN_SIGNING_KEY_LENGTH = 32;

    public static IServiceCollection AddResourceServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        bool allowTestingSigningKey = environment.IsEnvironment("Testing");

        services
            .AddOptions<JwtValidationOptions>()
            .Bind(configuration.GetSection(JWT_SECTION_NAME))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required")
            .Validate(
                options => HasValidSigningSource(
                    options,
                    environment,
                    allowTestingSigningKey),
                "Jwt requires an HTTPS MetadataAddress; " +
                "HTTP metadata is limited to Docker/Testing; " +
                $"only Testing may use a SigningKey with at least {MIN_SIGNING_KEY_LENGTH} characters")
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtValidationOptions>>((options, jwtOptions) =>
            {
                options.MapInboundClaims = false;
                ConfigureJwtBearer(
                    options,
                    jwtOptions.Value,
                    allowTestingSigningKey);
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                DirectoryAuthorizationPolicies.DIRECTORY_READ,
                policy => policy.RequireClaim(PERMISSION_CLAIM, DirectoryAuthorizationPolicies.DIRECTORY_READ));
            options.AddPolicy(
                DirectoryAuthorizationPolicies.DIRECTORY_MANAGE,
                policy => policy.RequireClaim(PERMISSION_CLAIM, DirectoryAuthorizationPolicies.DIRECTORY_MANAGE));
        });

        return services;
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions bearerOptions,
        JwtValidationOptions jwtOptions,
        bool allowTestingSigningKey)
    {
        bearerOptions.TokenValidationParameters = CreateTokenValidationParameters(jwtOptions);

        if (string.IsNullOrWhiteSpace(jwtOptions.MetadataAddress))
        {
            if (!allowTestingSigningKey)
            {
                return;
            }

            // Symmetric key остаётся только быстрым test helper для изолированных integration tests.
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

    private static bool HasValidSigningSource(
        JwtValidationOptions options,
        IHostEnvironment environment,
        bool allowTestingSigningKey)
    {
        if (!string.IsNullOrWhiteSpace(options.MetadataAddress))
        {
            if (!Uri.TryCreate(
                    options.MetadataAddress,
                    UriKind.Absolute,
                    out Uri? metadataAddress))
            {
                return false;
            }

            if (options.RequireHttpsMetadata)
            {
                return metadataAddress.Scheme == Uri.UriSchemeHttps;
            }

            return (environment.IsEnvironment("Docker") ||
                    allowTestingSigningKey) &&
                metadataAddress.Scheme == Uri.UriSchemeHttp;
        }

        return allowTestingSigningKey &&
            !string.IsNullOrWhiteSpace(options.SigningKey) &&
            options.SigningKey.Length >= MIN_SIGNING_KEY_LENGTH;
    }

    private sealed class JwtValidationOptions
    {
        public string Issuer { get; init; } = string.Empty;
        public string Audience { get; init; } = string.Empty;

        // Используется только integration tests при EnvironmentName=Testing.
        public string SigningKey { get; init; } = string.Empty;
        public string MetadataAddress { get; init; } = string.Empty;
        public bool RequireHttpsMetadata { get; init; } = true;
    }
}
