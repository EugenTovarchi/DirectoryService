namespace AuthService.Web.Authentication;

/// <summary>
/// Имена отдельной browser authentication scheme и cookie для OIDC pages.
/// Resource API по-прежнему использует Bearer JWT и не принимает эту cookie.
/// </summary>
public static class OidcBrowserAuthenticationDefaults
{
    public const string SCHEME = "OidcBrowser";
    public const string COOKIE_NAME = "__Host-24eye-oidc";
}
