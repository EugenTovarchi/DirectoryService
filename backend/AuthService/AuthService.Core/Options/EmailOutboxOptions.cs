namespace AuthService.Core.Options;

/// <summary>
/// Настройки фоновой доставки email outbox и экспоненциальных повторных попыток.
/// </summary>
public sealed class EmailOutboxOptions
{
    public const string SECTION_NAME = "EmailOutbox";

    /// <summary>Включает регистрацию периодического Quartz-задания.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Интервал между проверками готовых outbox-записей.</summary>
    public int PollIntervalSeconds { get; init; } = 5;

    /// <summary>Максимальное число писем, которое один запуск Quartz job забирает из outbox.</summary>
    public int MessagesPerRun { get; init; } = 10;

    /// <summary>Число попыток доставки до окончательного отказа и очистки secret-bearing payload.</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Начальная задержка перед первой повторной попыткой.</summary>
    public int InitialRetryDelaySeconds { get; init; } = 30;

    /// <summary>Верхняя граница задержки exponential backoff.</summary>
    public int MaxRetryDelaySeconds { get; init; } = 900;

    /// <summary>Время резерва записи до её повторного подбора после падения процесса.</summary>
    public int ProcessingLeaseSeconds { get; init; } = 120;
}
