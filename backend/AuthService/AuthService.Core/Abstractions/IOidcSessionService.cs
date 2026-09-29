namespace AuthService.Core.Abstractions;

/// <summary>
/// Работает с пользовательскими OAuth/OIDC sessions через OpenIddict storage.
/// Одна session соответствует authorization/grant с активным refresh token.
/// </summary>
public interface IOidcSessionService
{
    /// <summary>
    /// Возвращает только sessions с действующим refresh token.
    /// Access и ID tokens отдельными sessions не считаются.
    /// </summary>
    Task<IReadOnlyList<OidcSession>> GetActiveSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отзывает одну session, только если она принадлежит указанному пользователю.
    /// Неизвестная или чужая session обрабатывается идемпотентно.
    /// </summary>
    Task<bool> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отзывает все OpenIddict authorizations и tokens пользователя.
    /// Уже выданные JWT могут приниматься resource services до окончания lifetime.
    /// </summary>
    Task RevokeAllSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Безопасная проекция OpenIddict session для application layer.
/// OpenIddict не хранит IP и User-Agent, поэтому эти данные сюда не добавляются.
/// </summary>
public sealed record OidcSession(
    Guid Id,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastUsedAt);
