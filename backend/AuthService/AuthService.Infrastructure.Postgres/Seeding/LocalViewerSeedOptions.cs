namespace AuthService.Infrastructure.Postgres.Seeding;

/// <summary>
/// Legacy-настройки одного локального Viewer.
/// Секция сохранена для совместимости с существующим Docker environment.
/// Для нескольких ролей используйте <see cref="LocalUsersSeedOptions"/>.
/// </summary>
public sealed class LocalViewerSeedOptions
{
    public const string SECTION_NAME = "LocalViewerSeed";

    public bool Enabled { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string DisplayName { get; init; } = "Local Viewer";
}
