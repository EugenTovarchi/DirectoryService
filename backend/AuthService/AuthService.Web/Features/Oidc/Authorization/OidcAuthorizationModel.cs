namespace AuthService.Web.Features.Oidc.Authorization;

/// <summary>
/// Данные consent page: какой client и какие scopes запрашивает.
/// </summary>
public sealed record OidcAuthorizationModel(
    string ClientDisplayName,
    IReadOnlyCollection<string> Scopes,
    string ClientId,
    string RedirectUri,
    string ResponseType,
    string Scope,
    string? State,
    string CodeChallenge,
    string CodeChallengeMethod);
