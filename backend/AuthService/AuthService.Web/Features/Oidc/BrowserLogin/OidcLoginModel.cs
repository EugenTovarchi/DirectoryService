using System.ComponentModel.DataAnnotations;

namespace AuthService.Web.Features.Oidc.BrowserLogin;

/// <summary>
/// Поля минимальной browser login form для OIDC authorization session.
/// </summary>
public sealed class OidcLoginModel
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; init; } = string.Empty;

    [Required]
    public string ReturnUrl { get; init; } = string.Empty;

    public string? ErrorMessage { get; set; }
}
