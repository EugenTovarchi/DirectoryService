using System.Security.Claims;
using AuthService.Core.Abstractions;
using AuthService.Core.Options;
using AuthService.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace AuthService.Web.Features.Oidc.UserInfo;

/// <summary>
/// Строит UserInfo response из валидированного access token и актуального Identity user.
/// </summary>
public sealed class OidcUserInfoHandler
{
    private readonly IRolePermissionReader _rolePermissionReader;
    private readonly UserManager<ApplicationUser> _userManager;

    public OidcUserInfoHandler(
        IRolePermissionReader rolePermissionReader,
        UserManager<ApplicationUser> userManager)
    {
        _rolePermissionReader = rolePermissionReader;
        _userManager = userManager;
    }

    /// <summary>
    /// Перечитывает активного Identity user и формирует минимальный scoped response.
    /// Roles, permissions и company context возвращаются только при наличии scope <c>auth</c>.
    /// </summary>
    public async Task<OidcUserInfoResponse?> Handle(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        string? subject = principal.GetClaim(OpenIddictConstants.Claims.Subject);
        if (string.IsNullOrWhiteSpace(subject))
            return null;

        ApplicationUser? user = await _userManager.FindByIdAsync(subject);
        if (user is not { IsActive: true })
            return null;

        string[]? roles = null;
        IReadOnlyCollection<string>? permissions = null;

        if (principal.HasScope(OidcScopes.AUTH))
        {
            roles = (await _userManager.GetRolesAsync(user)).ToArray();
            permissions = await _rolePermissionReader.GetPermissionCodesAsync(
                roles,
                cancellationToken);
        }

        return new OidcUserInfoResponse
        {
            Subject = user.Id.ToString(),
            Name = principal.HasScope(OidcScopes.PROFILE)
                ? user.DisplayName?.Value ?? user.UserName
                : null,
            PreferredUsername = principal.HasScope(OidcScopes.PROFILE)
                ? user.UserName
                : null,
            Email = principal.HasScope(OidcScopes.EMAIL)
                ? user.Email
                : null,
            EmailVerified = principal.HasScope(OidcScopes.EMAIL)
                ? user.EmailConfirmed
                : null,
            CompanyId = principal.HasScope(OidcScopes.AUTH)
                ? user.CurrentCompanyId?.ToString()
                : null,
            Roles = roles,
            Permissions = permissions
        };
    }
}
