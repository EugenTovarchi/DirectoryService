using System.Security.Claims;
using AuthService.Core.Abstractions;
using AuthService.Core.Authorization;
using AuthService.Core.Options;
using AuthService.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace AuthService.Web.Features.Oidc.Authorization;

/// <summary>
/// Загружает Identity user/client и строит principal, из которого OpenIddict выпустит code и tokens.
/// HTTP redirects, consent form и protocol response остаются в controller.
/// </summary>
public sealed class OidcAuthorizationHandler
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IRolePermissionReader _rolePermissionReader;
    private readonly UserManager<ApplicationUser> _userManager;

    public OidcAuthorizationHandler(
        IOpenIddictApplicationManager applicationManager,
        IRolePermissionReader rolePermissionReader,
        UserManager<ApplicationUser> userManager)
    {
        _applicationManager = applicationManager;
        _rolePermissionReader = rolePermissionReader;
        _userManager = userManager;
    }

    /// <summary>
    /// Загружает зарегистрированный client и готовит только данные, необходимые consent page.
    /// Метод не принимает решение за пользователя и не выпускает tokens.
    /// </summary>
    public async Task<OidcAuthorizationModel?> GetConsentAsync(
        OpenIddictRequest request,
        ClaimsPrincipal browserPrincipal,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await FindActiveUserAsync(browserPrincipal);
        if (user is null || string.IsNullOrWhiteSpace(request.ClientId))
            return null;

        object? application = await _applicationManager.FindByClientIdAsync(
            request.ClientId,
            cancellationToken);
        if (application is null)
            return null;

        string? displayName = await _applicationManager.GetDisplayNameAsync(
            application,
            cancellationToken);

        return new OidcAuthorizationModel(
            displayName ?? request.ClientId,
            request.GetScopes(),
            request.ClientId,
            request.RedirectUri ?? string.Empty,
            request.ResponseType ?? string.Empty,
            request.Scope ?? string.Empty,
            request.State,
            request.CodeChallenge ?? string.Empty,
            request.CodeChallengeMethod ?? string.Empty);
    }

    /// <summary>
    /// Строит claims principal для принятого request.
    /// OpenIddict использует scopes, resources и destinations этого principal при выпуске tokens.
    /// </summary>
    public async Task<ClaimsPrincipal?> CreateTokenPrincipalAsync(
        OpenIddictRequest request,
        ClaimsPrincipal browserPrincipal,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await FindActiveUserAsync(browserPrincipal);
        if (user is null)
            return null;

        string[] roles = (await _userManager.GetRolesAsync(user)).ToArray();
        IReadOnlyCollection<string> permissions =
            await _rolePermissionReader.GetPermissionCodesAsync(roles, cancellationToken);

        var identity = new ClaimsIdentity(
            authenticationType: "OpenIddict",
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        identity.SetClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());
        identity.SetClaim(
            OpenIddictConstants.Claims.Name,
            user.DisplayName?.Value ?? user.UserName);
        identity.SetClaim(
            OpenIddictConstants.Claims.PreferredUsername,
            user.UserName);
        identity.SetClaim(OpenIddictConstants.Claims.Email, user.Email);
        identity.AddClaim(new Claim(
            OpenIddictConstants.Claims.EmailVerified,
            user.EmailConfirmed ? "true" : "false",
            ClaimValueTypes.Boolean));

        foreach (string role in roles)
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, role));
        }

        foreach (string permission in permissions)
        {
            identity.AddClaim(new Claim(AuthClaimTypes.PERMISSION, permission));
        }

        if (user.CurrentCompanyId is Guid companyId)
            identity.SetClaim(AuthClaimTypes.COMPANY_ID, companyId.ToString());

        identity.SetScopes(request.GetScopes());
        identity.SetResources(OidcScopes.GetResources(request.GetScopes()));
        identity.SetDestinations(GetDestinations);

        return new ClaimsPrincipal(identity);
    }

    private async Task<ApplicationUser?> FindActiveUserAsync(ClaimsPrincipal principal)
    {
        string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out Guid parsedUserId))
            return null;

        ApplicationUser? user = await _userManager.FindByIdAsync(parsedUserId.ToString());
        return user is { IsActive: true } ? user : null;
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        ClaimsIdentity? identity = claim.Subject;
        if (identity is null)
        {
            yield break;
        }

        if (claim.Type == OpenIddictConstants.Claims.Subject)
        {
            yield return OpenIddictConstants.Destinations.AccessToken;
            yield return OpenIddictConstants.Destinations.IdentityToken;
            yield break;
        }

        bool isProfileClaim =
            claim.Type == OpenIddictConstants.Claims.Name ||
            claim.Type == OpenIddictConstants.Claims.PreferredUsername;
        if (isProfileClaim && identity.HasScope(OidcScopes.PROFILE))
        {
            yield return OpenIddictConstants.Destinations.IdentityToken;
            yield break;
        }

        bool isEmailClaim =
            claim.Type == OpenIddictConstants.Claims.Email ||
            claim.Type == OpenIddictConstants.Claims.EmailVerified;
        if (isEmailClaim && identity.HasScope(OidcScopes.EMAIL))
        {
            yield return OpenIddictConstants.Destinations.IdentityToken;
            yield break;
        }

        if (claim.Type == OpenIddictConstants.Claims.Role &&
            HasServiceScope(identity))
        {
            yield return OpenIddictConstants.Destinations.AccessToken;

            if (identity.HasScope(OidcScopes.AUTH))
            {
                yield return OpenIddictConstants.Destinations.IdentityToken;
            }

            yield break;
        }

        if (claim.Type == AuthClaimTypes.COMPANY_ID &&
            HasServiceScope(identity))
        {
            yield return OpenIddictConstants.Destinations.AccessToken;

            if (identity.HasScope(OidcScopes.AUTH))
            {
                yield return OpenIddictConstants.Destinations.IdentityToken;
            }

            yield break;
        }

        if (claim.Type == AuthClaimTypes.PERMISSION &&
            HasServiceScope(identity))
        {
            yield return OpenIddictConstants.Destinations.AccessToken;
        }
    }

    private static bool HasServiceScope(ClaimsIdentity identity)
    {
        return identity.HasScope(OidcScopes.DIRECTORY) ||
            identity.HasScope(OidcScopes.FILES) ||
            identity.HasScope(OidcScopes.AUTH);
    }
}
