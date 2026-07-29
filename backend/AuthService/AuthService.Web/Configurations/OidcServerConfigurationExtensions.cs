using System.Security.Cryptography.X509Certificates;
using AuthService.Core.Options;
using AuthService.Domain.Identity;
using AuthService.Infrastructure.Postgres;
using AuthService.Infrastructure.Postgres.Seeding;
using AuthService.Web.Features.Oidc.Authorization;
using AuthService.Web.Features.Oidc.BrowserLogin;
using AuthService.Web.Features.Oidc.Token;
using AuthService.Web.Features.Oidc.UserInfo;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace AuthService.Web.Configurations;

/// <summary>
/// Собирает OpenIddict authorization server поверх существующих Identity и PostgreSQL.
/// Здесь задаётся protocol surface; обработчики login/consent остаются отдельными application slices.
/// </summary>
public static class OidcServerConfigurationExtensions
{
    /// <summary>
    /// Регистрирует OpenIddict stores, OAuth/OIDC endpoints, grants, scopes и cryptographic keys.
    /// Регистрация выключаема, чтобы legacy JWT endpoints продолжали работать во время поэтапной миграции.
    /// </summary>
    public static IServiceCollection AddAuthServiceOidcServer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Bind превращает секцию "Oidc" из appsettings/environment variables
        // в типизированный объект. ValidateOnStart останавливает запуск при ошибке,
        // а не откладывает её до первого OAuth request.
        services.AddSingleton<IValidateOptions<OidcServerOptions>, OidcServerOptionsValidator>();
        services.AddOptions<OidcServerOptions>()
            .Bind(configuration.GetSection(OidcServerOptions.SECTION_NAME))
            .ValidateOnStart();

        OidcServerOptions serverOptions = configuration
            .GetSection(OidcServerOptions.SECTION_NAME)
            .Get<OidcServerOptions>() ?? new OidcServerOptions();

        if (!serverOptions.Enabled)
            return services;

        // AddCore подключает внутренние OpenIddict managers и хранилище.
        // AddServer подключает сам OAuth/OIDC protocol engine.
        services.AddOpenIddict()
            .AddCore(options =>
            {
                // OpenIddict хранит applications, grants, scopes и tokens в том же PostgreSQL,
                // но не заменяет таблицы ASP.NET Core Identity с users/roles.
                options.UseEntityFrameworkCore()
                    .UseDbContext<AuthServiceDbContext>();
            })
            .AddServer(options =>
            {
                // Эти адреса публикуются в discovery document.
                // Authorization/UserInfo requests передаются нашим тонким MVC controllers,
                // а token endpoint OpenIddict обрабатывает сам после проверки code или refresh token.
                options.SetIssuer(new Uri(serverOptions.Issuer, UriKind.Absolute));
                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.SetTokenEndpointUris("/connect/token");
                options.SetUserInfoEndpointUris("/connect/userinfo");

                // Authorization Code + PKCE — основной интерактивный flow.
                // Refresh grant продлевает пользовательскую сессию, а Client Credentials
                // выдаёт access token самому сервису без пользователя и browser.
                options.AllowAuthorizationCodeFlow();
                options.AllowRefreshTokenFlow();
                options.AllowClientCredentialsFlow();
                options.RequireProofKeyForCodeExchange();

                options.RegisterScopes(
                    OidcScopes.OPEN_ID,
                    OidcScopes.PROFILE,
                    OidcScopes.EMAIL,
                    OidcScopes.OFFLINE_ACCESS,
                    OidcScopes.DIRECTORY,
                    OidcScopes.FILES,
                    OidcScopes.AUTH);

                // Discovery document должен честно объявлять claims, которые могут попасть
                // в ID token, access token или UserInfo response.
                options.RegisterClaims(
                    OpenIddictConstants.Claims.Name,
                    OpenIddictConstants.Claims.PreferredUsername,
                    OpenIddictConstants.Claims.Email,
                    OpenIddictConstants.Claims.EmailVerified,
                    OpenIddictConstants.Claims.Role,
                    AuthClaimTypes.COMPANY_ID,
                    AuthClaimTypes.PERMISSION,
                    AuthClaimTypes.CLIENT_ID,
                    AuthClaimTypes.SERVICE_NAME,
                    AuthClaimTypes.SERVICE_PERMISSION);

                options.SetAccessTokenLifetime(
                    TimeSpan.FromMinutes(serverOptions.AccessTokenLifetimeMinutes));
                options.SetRefreshTokenLifetime(
                    TimeSpan.FromDays(serverOptions.RefreshTokenLifetimeDays));

                // Refresh token одноразовый: после успешного exchange старое значение
                // сразу становится недействительным. Нулевая leeway повышает защиту от replay,
                // но client не должен параллельно отправлять два refresh requests.
                options.SetRefreshTokenReuseLeeway(TimeSpan.Zero);

                // Resource services должны получать обычный подписанный JWT через JWKS.
                options.DisableAccessTokenEncryption();

                ConfigureCertificates(options, serverOptions, environment);

                OpenIddictServerAspNetCoreBuilder aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough();

                // HTTP разрешается только явной development/test настройкой.
                // Production validator запрещает отключать transport security.
                if (serverOptions.DisableTransportSecurityRequirement)
                {
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(options =>
            {
                // AuthService API находится в том же process, что authorization server.
                // UseLocalServer импортирует issuer и cryptographic keys напрямую,
                // поэтому сервису не нужно обращаться к собственному discovery endpoint.
                options.UseLocalServer();

                // AuthService принимает только access token, предназначенный ему.
                // Token с aud=directory-service или aud=file-service будет отклонён.
                options.AddAudiences(OidcScopes.AUTH_RESOURCE);

                // Подключает OpenIddict validation handler к ASP.NET Core authentication pipeline.
                options.UseAspNetCore();
            });

        // Seeder создаёт только отсутствующие scopes/clients после применения migrations.
        services.AddScoped<OidcServerSeeder>();
        services.AddScoped<OidcAuthorizationHandler>();
        services.AddScoped<OidcBrowserLoginHandler>();
        services.AddScoped<OidcTokenHandler>();
        services.AddScoped<OidcUserInfoHandler>();

        return services;
    }

    /// <summary>
    /// Выбирает ключи по environment:
    /// ephemeral для изолированных tests, development certificates для local run
    /// и явно переданные PFX certificates для production.
    /// </summary>
    private static void ConfigureCertificates(
        OpenIddictServerBuilder options,
        OidcServerOptions serverOptions,
        IHostEnvironment environment)
    {
        if (environment.IsEnvironment("Testing"))
        {
            options.AddEphemeralEncryptionKey();
            options.AddEphemeralSigningKey();
            return;
        }

        if (serverOptions.UseDevelopmentCertificates)
        {
            options.AddDevelopmentEncryptionCertificate();
            options.AddDevelopmentSigningCertificate();
            return;
        }

        X509Certificate2 signingCertificate = X509CertificateLoader.LoadPkcs12FromFile(
            serverOptions.SigningCertificatePath,
            serverOptions.SigningCertificatePassword);
        X509Certificate2 encryptionCertificate = X509CertificateLoader.LoadPkcs12FromFile(
            serverOptions.EncryptionCertificatePath,
            serverOptions.EncryptionCertificatePassword);

        options.AddSigningCertificate(signingCertificate);
        options.AddEncryptionCertificate(encryptionCertificate);
    }
}
