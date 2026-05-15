namespace AuthService.Domain.Identity;

/// <summary>
/// Стартовый набор permissions для policy-based authorization.
/// </summary>
public static class AuthPermissions
{
    public const string USERS_MANAGE = "users.manage";
    public const string DIRECTORY_READ = "directory.read";
    public const string DIRECTORY_MANAGE = "directory.manage";
    public const string FILES_READ = "files.read";
    public const string FILES_UPLOAD = "files.upload";
    public const string VIDEOS_READ = "videos.read";
    public const string VIDEOS_UPLOAD = "videos.upload";
}
