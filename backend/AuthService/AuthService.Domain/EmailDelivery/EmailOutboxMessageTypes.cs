namespace AuthService.Domain.EmailDelivery;

/// <summary>
/// Типы писем, которые AuthService умеет доставлять через transactional outbox.
/// Строковые значения сохраняют формат БД стабильным при добавлении новых типов писем.
/// </summary>
public static class EmailOutboxMessageTypes
{
    public const string INVITE = "invite";
    public const string PASSWORD_RESET = "password-reset";
}
