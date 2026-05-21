namespace AuthService.Domain.Identity;

/// <summary>
/// Стартовый набор permissions для policy-based authorization.
/// </summary>
public static class AuthPermissions
{
    /// <summary>
    /// Позволяет администрировать пользователей: приглашать, просматривать, менять статус и роли в разрешенной области.
    /// </summary>
    public const string USERS_MANAGE = "users.manage";

    /// <summary>
    /// Позволяет читать структуру компании и справочные данные DirectoryService.
    /// </summary>
    public const string DIRECTORY_READ = "directory.read";

    /// <summary>
    /// Позволяет создавать и изменять структуру компании в DirectoryService.
    /// </summary>
    public const string DIRECTORY_MANAGE = "directory.manage";

    /// <summary>
    /// Позволяет читать метаданные файлов и получать данные для скачивания.
    /// </summary>
    public const string FILES_READ = "files.read";

    /// <summary>
    /// Позволяет запускать сценарии загрузки файлов.
    /// </summary>
    public const string FILES_UPLOAD = "files.upload";

    /// <summary>
    /// Позволяет читать метаданные видео и данные для просмотра HLS.
    /// </summary>
    public const string VIDEOS_READ = "videos.read";

    /// <summary>
    /// Позволяет запускать сценарии загрузки видео.
    /// </summary>
    public const string VIDEOS_UPLOAD = "videos.upload";
}
