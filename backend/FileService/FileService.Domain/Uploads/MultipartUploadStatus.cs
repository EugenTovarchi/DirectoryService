namespace FileService.Domain.Uploads;

/// <summary>
/// Техническое состояние multipart-загрузки. Оно описывает работу с S3
/// и не заменяет бизнес-состояние файла <c>MediaStatus</c>.
/// </summary>
public enum MultipartUploadStatus
{
    /// <summary>Сессия создана локально, но S3 upload ещё не зарегистрирован.</summary>
    INITIALIZING,

    /// <summary>Сессия готова принимать части файла.</summary>
    ACTIVE,

    /// <summary>Один из экземпляров сервиса получил право завершить загрузку.</summary>
    COMPLETING,

    /// <summary>S3 upload завершён, итоговое состояние сохранено локально.</summary>
    COMPLETED,

    /// <summary>Один из экземпляров сервиса получил право отменить или очистить загрузку.</summary>
    ABORTING,

    /// <summary>Загрузка отменена пользователем.</summary>
    ABORTED,

    /// <summary>Загрузка завершилась невосстановимой ошибкой.</summary>
    FAILED,

    /// <summary>Истёкшая загрузка была очищена фоновым процессом.</summary>
    EXPIRED
}
