namespace DirectoryService.SharedKernel;

public record Error
{
    public const string SEPARATOR = "||";
    public string Code { get; }
    public string Message { get; }
    public ErrorType? Type { get; }
    public string? InvalidField { get; }

    private Error(string code, string message, ErrorType? type, string? invalidField = null)
    {
        Code = code;
        Message = message;
        Type = type;
        InvalidField = invalidField;
    }

    public static Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string code, string message, string? invalidField = null) =>
        new(code ?? "value.is.invalid", message, ErrorType.Validation);
    public static Error NotFound(string code, string message) =>
        new(code ?? "record.not.found", message, ErrorType.NotFound);
    public static Error Failure(string code, string message) =>
        new(code ?? "failure", message, ErrorType.Failure);
    public static Error Conflict(string code, string message) =>
        new(code ?? "value.is.conflict", message, ErrorType.Conflict);
    public static Error Authorization(string code, string message) =>
        new(code ?? "authoruzation.error", message, ErrorType.Authorization);
    public static Error Authentication(string code, string message) =>
        new(code ?? "authentication.error", message, ErrorType.Authentication);

    public Failure ToFailure() => new([this]);

    /// <summary>
    /// Разделяем код ошибки, сообщение ошибки и тип ошибки в строке
    /// </summary>
    /// <returns></returns>
    public string Serialize()
    {
        return string.Join(SEPARATOR, Code, Message, Type);
    }


    /// Собираем ошибку из строки с данными ошибки
    public static Error Deserialize(string serialized)
    {
        var parts = serialized.Split(SEPARATOR);

        if (parts.Length < 3)
        {
            throw new ArgumentException("Invalid serialized format");
        }

        if (Enum.TryParse<ErrorType>(parts[2], out var type) == false)
        {
            throw new ArgumentException("Invalid serialized format");
        }

        return new Error(parts[0], parts[1], type);
    }
}
public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Failure,
    Conflict,
    Authorization,
    Authentication
}

