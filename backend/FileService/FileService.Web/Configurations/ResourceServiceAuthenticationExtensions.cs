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
    private const int MIN_SIGNING_KEY_LENGTH = 32;

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
                options => !string.IsNullOrWhiteSpace(options.SigningKey) &&
                    options.SigningKey.Length >= MIN_SIGNING_KEY_LENGTH,
                $"Jwt:SigningKey must be at least {MIN_SIGNING_KEY_LENGTH} characters")
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtValidationOptions>>((options, jwtOptions) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = CreateTokenValidationParameters(jwtOptions.Value);
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
        });

        return services;
    }

    private static TokenValidationParameters CreateTokenValidationParameters(JwtValidationOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    private sealed class JwtValidationOptions
    {
        public string Issuer { get; init; } = string.Empty;
        public string Audience { get; init; } = string.Empty;
        public string SigningKey { get; init; } = string.Empty;
    }
}
