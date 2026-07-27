namespace FileService.Core.FilesStorage;

/// <summary>
/// Настройки жизненного цикла multipart-сессий и восстановления операций между экземплярами сервиса.
/// </summary>
public sealed class MultipartUploadOptions
{
    public const string SECTION_NAME = "MultipartUploadOptions";

    /// <summary>Срок, в течение которого клиент может загружать части файла.</summary>
    public int SessionExpirationHours { get; init; } = 24;

    /// <summary>
    /// Время, в течение которого операция закреплена за одним экземпляром сервиса.
    /// После него другой экземпляр может продолжить
    /// зависшую операцию COMPLETING или ABORTING.
    /// </summary>
    public int OperationTimeoutMinutes { get; init; } = 5;

    /// <summary>Интервал запуска фоновой очистки просроченных multipart-загрузок.</summary>
    public int CleanupIntervalMinutes { get; init; } = 10;

    /// <summary>Максимальное количество сессий, обрабатываемых за один проход фоновой очистки.</summary>
    public int CleanupBatchSize { get; init; } = 100;
}
