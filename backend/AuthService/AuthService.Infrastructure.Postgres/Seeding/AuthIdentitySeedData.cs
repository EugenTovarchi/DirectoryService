using AuthService.Domain.Identity;

namespace AuthService.Infrastructure.Postgres.Seeding;

/// <summary>
/// Стартовая модель ролей и разрешений для MVP AuthService.
/// </summary>
internal static class AuthIdentitySeedData
{
    public static readonly IReadOnlyCollection<PermissionSeedItem> Permissions =
    [
        new(AuthPermissions.USERS_MANAGE, "Управление пользователями, ролями и статусами внутри разрешенной области."),
        new(AuthPermissions.DIRECTORY_READ, "Чтение структуры компании и связанных справочных данных."),
        new(AuthPermissions.DIRECTORY_MANAGE, "Создание и изменение структуры компании."),
        new(AuthPermissions.FILES_READ, "Чтение метаданных файлов и данных для скачивания."),
        new(AuthPermissions.FILES_UPLOAD, "Запуск сценариев загрузки файлов."),
        new(AuthPermissions.VIDEOS_READ, "Чтение метаданных видео и HLS-данных."),
        new(AuthPermissions.VIDEOS_UPLOAD, "Запуск сценариев загрузки видео.")
    ];

    public static readonly IReadOnlyCollection<RoleSeedItem> Roles =
    [
        new(AuthRoles.SYSTEM_ADMIN, "Администрирование платформы."),
        new(AuthRoles.COMPANY_ADMIN, "Администрирование пользователей и доступа внутри компании."),
        new(AuthRoles.OPERATOR, "Работа с камерами, видео и назначенными объектами компании."),
        new(AuthRoles.TECHNICIAN, "Техническое обслуживание камер, устройств и связанных процессов."),
        new(AuthRoles.VIEWER, "Доступ только на чтение к разрешенным объектам компании и медиа.")
    ];

    public static readonly IReadOnlyDictionary<string, string[]> RolePermissions =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [AuthRoles.SYSTEM_ADMIN] =
            [
                AuthPermissions.USERS_MANAGE,
                AuthPermissions.DIRECTORY_READ,
                AuthPermissions.DIRECTORY_MANAGE,
                AuthPermissions.FILES_READ,
                AuthPermissions.FILES_UPLOAD,
                AuthPermissions.VIDEOS_READ,
                AuthPermissions.VIDEOS_UPLOAD
            ],
            [AuthRoles.COMPANY_ADMIN] =
            [
                AuthPermissions.USERS_MANAGE,
                AuthPermissions.DIRECTORY_READ,
                AuthPermissions.DIRECTORY_MANAGE,
                AuthPermissions.FILES_READ,
                AuthPermissions.FILES_UPLOAD,
                AuthPermissions.VIDEOS_READ,
                AuthPermissions.VIDEOS_UPLOAD
            ],
            [AuthRoles.OPERATOR] =
            [
                AuthPermissions.DIRECTORY_READ,
                AuthPermissions.FILES_READ,
                AuthPermissions.FILES_UPLOAD,
                AuthPermissions.VIDEOS_READ,
                AuthPermissions.VIDEOS_UPLOAD
            ],
            [AuthRoles.TECHNICIAN] =
            [
                AuthPermissions.DIRECTORY_READ,
                AuthPermissions.FILES_READ,
                AuthPermissions.VIDEOS_READ,
                AuthPermissions.VIDEOS_UPLOAD
            ],
            [AuthRoles.VIEWER] =
            [
                AuthPermissions.DIRECTORY_READ,
                AuthPermissions.FILES_READ,
                AuthPermissions.VIDEOS_READ
            ]
        };
}

internal sealed record PermissionSeedItem(string Code, string Description);

internal sealed record RoleSeedItem(string Name, string Description);
