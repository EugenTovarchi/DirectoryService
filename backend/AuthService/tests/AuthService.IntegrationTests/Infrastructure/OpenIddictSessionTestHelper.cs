using AuthService.Infrastructure.Postgres.Seeding;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace AuthService.IntegrationTests.Infrastructure;

/// <summary>
/// Создаёт минимальные OpenIddict authorization/token records для session integration tests.
/// Browser login здесь не повторяется: полный protocol flow покрыт отдельным OIDC test.
/// </summary>
public static class OpenIddictSessionTestHelper
{
    private const string CLIENT_ID = "oidc-public-tests";

    /// <summary>
    /// Создаёт authorization и связанный refresh token.
    /// AuthorizationId является публичным session id.
    /// </summary>
    public static async Task<OpenIddictTestSession> CreateSessionAsync(
        IServiceProvider services,
        Guid userId,
        DateTimeOffset? expirationDate = null,
        bool revoked = false,
        DateTimeOffset? lastUsedAt = null)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        var seeder = scope.ServiceProvider.GetRequiredService<OidcServerSeeder>();
        await seeder.SeedAsync();

        var applicationManager = scope.ServiceProvider
            .GetRequiredService<IOpenIddictApplicationManager>();
        var authorizationManager = scope.ServiceProvider
            .GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokenManager = scope.ServiceProvider
            .GetRequiredService<IOpenIddictTokenManager>();

        object application = await applicationManager.FindByClientIdAsync(CLIENT_ID)
            ?? throw new InvalidOperationException("OIDC test client is missing");
        string applicationId = await applicationManager.GetIdAsync(application)
            ?? throw new InvalidOperationException("OIDC test client id is missing");

        DateTimeOffset creationDate = DateTimeOffset.UtcNow;
        object authorization = await authorizationManager.CreateAsync(
            new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = applicationId,
                CreationDate = creationDate,
                Status = revoked
                    ? OpenIddictConstants.Statuses.Revoked
                    : OpenIddictConstants.Statuses.Valid,
                Subject = userId.ToString(),
                Type = OpenIddictConstants.AuthorizationTypes.AdHoc
            });
        string authorizationId = await authorizationManager.GetIdAsync(authorization)
            ?? throw new InvalidOperationException("OIDC authorization id is missing");

        if (lastUsedAt is not null)
        {
            await tokenManager.CreateAsync(
                new OpenIddictTokenDescriptor
                {
                    ApplicationId = applicationId,
                    AuthorizationId = authorizationId,
                    CreationDate = creationDate,
                    ExpirationDate = expirationDate ?? creationDate.AddDays(30),
                    RedemptionDate = lastUsedAt,
                    Status = OpenIddictConstants.Statuses.Redeemed,
                    Subject = userId.ToString(),
                    Type = OpenIddictConstants.TokenTypeIdentifiers.RefreshToken
                });
        }

        object refreshToken = await tokenManager.CreateAsync(
            new OpenIddictTokenDescriptor
            {
                ApplicationId = applicationId,
                AuthorizationId = authorizationId,
                CreationDate = creationDate,
                ExpirationDate = expirationDate ?? creationDate.AddDays(30),
                Status = revoked
                    ? OpenIddictConstants.Statuses.Revoked
                    : OpenIddictConstants.Statuses.Valid,
                Subject = userId.ToString(),
                Type = OpenIddictConstants.TokenTypeIdentifiers.RefreshToken
            });
        string refreshTokenId = await tokenManager.GetIdAsync(refreshToken)
            ?? throw new InvalidOperationException("OIDC refresh token id is missing");

        return new OpenIddictTestSession(
            Guid.Parse(authorizationId),
            Guid.Parse(refreshTokenId));
    }

    /// <summary>
    /// Возвращает сохранённые статусы authorization и refresh token без чтения token payload.
    /// </summary>
    public static async Task<OpenIddictTestSessionStatus> GetStatusAsync(
        IServiceProvider services,
        OpenIddictTestSession session)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        var authorizationManager = scope.ServiceProvider
            .GetRequiredService<IOpenIddictAuthorizationManager>();
        var tokenManager = scope.ServiceProvider
            .GetRequiredService<IOpenIddictTokenManager>();

        object authorization = await authorizationManager.FindByIdAsync(
            session.AuthorizationId.ToString())
            ?? throw new InvalidOperationException("OIDC authorization is missing");
        object refreshToken = await tokenManager.FindByIdAsync(
            session.RefreshTokenId.ToString())
            ?? throw new InvalidOperationException("OIDC refresh token is missing");

        string? authorizationStatus = await authorizationManager.GetStatusAsync(authorization);
        string? refreshTokenStatus = await tokenManager.GetStatusAsync(refreshToken);

        return new OpenIddictTestSessionStatus(
            authorizationStatus,
            refreshTokenStatus);
    }
}

public sealed record OpenIddictTestSession(
    Guid AuthorizationId,
    Guid RefreshTokenId);

public sealed record OpenIddictTestSessionStatus(
    string? AuthorizationStatus,
    string? RefreshTokenStatus);
