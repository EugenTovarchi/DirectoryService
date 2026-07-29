using AuthService.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace AuthService.Infrastructure.Postgres.Seeding;

/// <summary>
/// Идемпотентно создаёт OpenIddict scopes и clients из Oidc configuration.
/// Повторный запуск не создаёт дубликаты и не меняет secret существующего client.
/// </summary>
public sealed class OidcServerSeeder
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly OidcServerOptions _options;
    private readonly ServiceClientOptions _serviceClientOptions;
    private readonly ILogger<OidcServerSeeder> _logger;

    public OidcServerSeeder(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        IOptions<OidcServerOptions> options,
        IOptions<ServiceClientOptions> serviceClientOptions,
        ILogger<OidcServerSeeder> logger)
    {
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
        _options = options.Value;
        _serviceClientOptions = serviceClientOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Сначала создаёт custom scopes, затем clients, которые могут их запрашивать.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedScopeAsync(
            OidcScopes.DIRECTORY,
            "DirectoryService API",
            "directory-service",
            cancellationToken);
        await SeedScopeAsync(
            OidcScopes.FILES,
            "FileService API",
            "file-service",
            cancellationToken);
        await SeedScopeAsync(
            OidcScopes.AUTH,
            "AuthService API",
            "auth-service",
            cancellationToken);

        foreach (OidcClientOptions client in _options.Clients)
        {
            await SeedInteractiveClientAsync(client, cancellationToken);
        }

        foreach (ServiceClientDefinition serviceClient in _serviceClientOptions.Clients)
        {
            await SeedServiceClientAsync(serviceClient, cancellationToken);
        }

        _logger.LogInformation("OpenIddict scopes and clients seed completed");
    }

    private async Task SeedScopeAsync(
        string name,
        string displayName,
        string resource,
        CancellationToken cancellationToken)
    {
        if (await _scopeManager.FindByNameAsync(name, cancellationToken) is not null)
            return;

        var descriptor = new OpenIddictScopeDescriptor
        {
            Name = name,
            DisplayName = displayName
        };
        descriptor.Resources.Add(resource);

        await _scopeManager.CreateAsync(descriptor, cancellationToken);
    }

    /// <summary>
    /// Регистрирует browser client для Authorization Code + PKCE и Refresh Token flows.
    /// </summary>
    private async Task SeedInteractiveClientAsync(
        OidcClientOptions client,
        CancellationToken cancellationToken)
    {
        object? existingApplication = await _applicationManager.FindByClientIdAsync(
            client.ClientId,
            cancellationToken);

        if (existingApplication is not null)
        {
            await EnsurePermissionAsync(
                existingApplication,
                OpenIddictConstants.Permissions.Endpoints.Revocation,
                cancellationToken);
            return;
        }

        bool isConfidential = !string.IsNullOrEmpty(client.ClientSecret);
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = isConfidential ? client.ClientSecret : null,
            ClientType = isConfidential
                ? OpenIddictConstants.ClientTypes.Confidential
                : OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = client.DisplayName
        };

        foreach (string redirectUri in client.RedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri, UriKind.Absolute));
        }

        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);

        foreach (string scope in client.AllowedScopes)
        {
            AddScopePermission(descriptor, scope);
        }

        await _applicationManager.CreateAsync(descriptor, cancellationToken);
    }

    /// <summary>
    /// Добавляет отсутствующее protocol permission уже созданному client.
    /// Это позволяет безопасно развивать конфигурацию без удаления client и его grants.
    /// </summary>
    private async Task EnsurePermissionAsync(
        object application,
        string permission,
        CancellationToken cancellationToken)
    {
        var descriptor = new OpenIddictApplicationDescriptor();
        await _applicationManager.PopulateAsync(
            descriptor,
            application,
            cancellationToken);

        if (!descriptor.Permissions.Add(permission))
        {
            return;
        }

        await _applicationManager.UpdateAsync(
            application,
            descriptor,
            cancellationToken);
    }

    /// <summary>
    /// Регистрирует confidential service client только для token endpoint и Client Credentials flow.
    /// Redirect URI и refresh token такому client не нужны, потому что пользователь не участвует.
    /// </summary>
    private async Task SeedServiceClientAsync(
        ServiceClientDefinition client,
        CancellationToken cancellationToken)
    {
        if (await _applicationManager.FindByClientIdAsync(client.ClientId, cancellationToken) is not null)
            return;

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = client.ClientSecret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName = client.ServiceName
        };

        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);

        foreach (string scope in client.AllowedScopes)
        {
            AddScopePermission(descriptor, scope);
        }

        await _applicationManager.CreateAsync(descriptor, cancellationToken);
    }

    private static void AddScopePermission(
        OpenIddictApplicationDescriptor descriptor,
        string scope)
    {
        // openid/offline_access OpenIddict обрабатывает отдельно и не требует client permission.
        if (scope is OidcScopes.OPEN_ID or OidcScopes.OFFLINE_ACCESS)
            return;

        descriptor.Permissions.Add(
            OpenIddictConstants.Permissions.Prefixes.Scope + scope);
    }
}
