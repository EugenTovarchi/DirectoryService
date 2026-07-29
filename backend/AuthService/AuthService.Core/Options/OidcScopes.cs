namespace AuthService.Core.Options;

/// <summary>
/// Единый список scopes, которые понимает AuthService.
/// Scope определяет, какие группы данных или API client просит использовать.
/// </summary>
public static class OidcScopes
{
    public const string OPEN_ID = "openid";
    public const string PROFILE = "profile";
    public const string EMAIL = "email";
    public const string OFFLINE_ACCESS = "offline_access";
    public const string DIRECTORY = "directory";
    public const string FILES = "files";
    public const string AUTH = "auth";

    // Это таблица маршрутизации access token:
    // requested scope определяет resource service, который увидит себя в claim aud.
    private static readonly Dictionary<string, string> ResourcesByScope =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DIRECTORY] = "directory-service",
            [FILES] = "file-service",
            [AUTH] = "auth-service"
        };

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        new[]
        {
            OPEN_ID,
            PROFILE,
            EMAIL,
            OFFLINE_ACCESS,
            DIRECTORY,
            FILES,
            AUTH
        },
        StringComparer.Ordinal);

    /// <summary>
    /// Преобразует выданные API scopes в audiences (resources) итогового access token.
    /// Resource service принимает token только тогда, когда его имя присутствует в aud.
    /// </summary>
    public static List<string> GetResources(IEnumerable<string> scopes)
    {
        var resources = new List<string>();

        foreach (string scope in scopes)
        {
            if (ResourcesByScope.TryGetValue(scope, out string? resource))
            {
                resources.Add(resource);
            }
        }

        return resources;
    }

    /// <summary>
    /// Определяет, является ли scope доступом к backend API, а не пользовательским OIDC scope.
    /// Machine-to-machine client может запрашивать только такие scopes.
    /// </summary>
    public static bool IsApiScope(string scope)
    {
        return ResourcesByScope.ContainsKey(scope);
    }
}
