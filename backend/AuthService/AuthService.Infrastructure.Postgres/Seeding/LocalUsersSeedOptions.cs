namespace AuthService.Infrastructure.Postgres.Seeding;

/// <summary>
/// Настройки opt-in пользователей для локальной разработки.
/// Секция выключена по умолчанию и разрешена только в Development, Docker и Testing.
/// </summary>
public sealed class LocalUsersSeedOptions
{
    public const string SECTION_NAME = "LocalUsersSeed";

    /// <summary>
    /// Явно включает создание настроенных локальных пользователей.
    /// Одного наличия списка недостаточно, чтобы случайно создать accounts.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Общий password для local users. Удобен только для development-среды
    /// и должен приходить из runtime configuration.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Пользователи для проверки разных ролей через Swagger, Postman или OIDC flow.
    /// Email и Password должны приходить из runtime configuration, а не из committed appsettings.
    /// </summary>
    public List<LocalUserSeedDefinition> Users { get; init; } =
        new List<LocalUserSeedDefinition>();
}

/// <summary>
/// Описание одного локального пользователя и его ожидаемой роли.
/// Seeder не меняет пароль уже существующего пользователя.
/// </summary>
public sealed class LocalUserSeedDefinition
{
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Необязательный индивидуальный password. Если пусто, используется общий
    /// <see cref="LocalUsersSeedOptions.Password"/>.
    /// </summary>
    public string Password { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Текущий company context. Для SystemAdmin может оставаться null.
    /// Для company-scoped ролей лучше использовать id локальной тестовой компании.
    /// </summary>
    public Guid? CurrentCompanyId { get; init; }
}
