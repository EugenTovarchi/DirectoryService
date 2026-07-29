using AuthService.Core.Abstractions;
using OpenIddict.Abstractions;

namespace AuthService.Infrastructure.Postgres.Services;

/// <summary>
/// Представляет OpenIddict authorization с активным refresh token как пользовательскую session.
/// AuthorizationId используется как стабильный session id при ротации refresh token.
/// </summary>
public sealed class OpenIddictSessionService : IOidcSessionService
{
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IOpenIddictTokenManager _tokenManager;

    public OpenIddictSessionService(
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictTokenManager tokenManager)
    {
        _authorizationManager = authorizationManager;
        _tokenManager = tokenManager;
    }

    /// <summary>
    /// Возвращает authorizations пользователя, у которых есть действующий refresh token.
    /// Authorization codes, access/ID tokens и отозванные refresh tokens в список не входят.
    /// </summary>
    public async Task<IReadOnlyList<OidcSession>> GetActiveSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        string subject = userId.ToString();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var sessions = new List<OidcSession>();
        var addedAuthorizationIds = new HashSet<Guid>();

        IAsyncEnumerable<object> refreshTokens = _tokenManager.FindAsync(
            subject,
            client: null,
            OpenIddictConstants.Statuses.Valid,
            OpenIddictConstants.TokenTypeIdentifiers.RefreshToken,
            cancellationToken);

        // Сначала завершаем database query, затем выполняем дополнительные lookups.
        // Npgsql не разрешает второй command, пока первый async reader ещё открыт.
        var activeRefreshTokens = new List<object>();
        await foreach (object refreshToken in refreshTokens)
        {
            activeRefreshTokens.Add(refreshToken);
        }

        foreach (object refreshToken in activeRefreshTokens)
        {
            DateTimeOffset? expirationDate = await _tokenManager.GetExpirationDateAsync(
                refreshToken,
                cancellationToken);
            if (expirationDate is null || expirationDate <= now)
            {
                continue;
            }

            string? authorizationId = await _tokenManager.GetAuthorizationIdAsync(
                refreshToken,
                cancellationToken);
            if (!Guid.TryParse(authorizationId, out Guid sessionId) ||
                !addedAuthorizationIds.Add(sessionId))
            {
                continue;
            }

            object? authorization = await _authorizationManager.FindByIdAsync(
                authorizationId,
                cancellationToken);
            if (authorization is null ||
                !await _authorizationManager.HasStatusAsync(
                    authorization,
                    OpenIddictConstants.Statuses.Valid,
                    cancellationToken))
            {
                continue;
            }

            DateTimeOffset? creationDate = await _authorizationManager.GetCreationDateAsync(
                authorization,
                cancellationToken);
            if (creationDate is null)
            {
                continue;
            }

            DateTimeOffset? lastUsedAt = await GetLastUsedAtAsync(
                authorizationId,
                cancellationToken);

            sessions.Add(new OidcSession(
                sessionId,
                creationDate.Value.UtcDateTime,
                expirationDate.Value.UtcDateTime,
                lastUsedAt?.UtcDateTime));
        }

        return sessions
            .OrderByDescending(session => session.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Отзывает authorization и все связанные с ним tokens.
    /// Проверка subject не позволяет пользователю отозвать чужую session по известному Guid.
    /// </summary>
    public async Task<bool> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        string authorizationId = sessionId.ToString();
        object? authorization = await _authorizationManager.FindByIdAsync(
            authorizationId,
            cancellationToken);
        if (authorization is null)
        {
            return false;
        }

        string? subject = await _authorizationManager.GetSubjectAsync(
            authorization,
            cancellationToken);
        if (!string.Equals(subject, userId.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        bool authorizationRevoked = await _authorizationManager.TryRevokeAsync(
            authorization,
            cancellationToken);
        if (!authorizationRevoked)
        {
            return false;
        }

        await _tokenManager.RevokeByAuthorizationIdAsync(
            authorizationId,
            cancellationToken);

        return true;
    }

    /// <summary>
    /// Отзывает все authorizations и tokens, связанные с subject пользователя.
    /// </summary>
    public async Task RevokeAllSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        string subject = userId.ToString();

        await _authorizationManager.RevokeBySubjectAsync(
            subject,
            cancellationToken);
        await _tokenManager.RevokeBySubjectAsync(
            subject,
            cancellationToken);
    }

    /// <summary>
    /// Находит время последнего использования refresh token внутри authorization chain.
    /// При первой выдаче refresh token значение отсутствует.
    /// </summary>
    private async Task<DateTimeOffset?> GetLastUsedAtAsync(
        string authorizationId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset? lastUsedAt = null;
        IAsyncEnumerable<object> authorizationTokens =
            _tokenManager.FindByAuthorizationIdAsync(
                authorizationId,
                cancellationToken);

        var tokens = new List<object>();
        await foreach (object token in authorizationTokens)
        {
            tokens.Add(token);
        }

        foreach (object token in tokens)
        {
            DateTimeOffset? redemptionDate = await _tokenManager.GetRedemptionDateAsync(
                token,
                cancellationToken);

            if (redemptionDate is null)
            {
                continue;
            }

            if (lastUsedAt is null || redemptionDate > lastUsedAt)
            {
                lastUsedAt = redemptionDate;
            }
        }

        return lastUsedAt;
    }
}
