using System.Text.Json.Serialization;

namespace DirectoryService.SharedKernel;

public record Error
{
    private const string SEPARATOR = "||";
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

    public static Error None = new(string.Empty, string.Empty, ErrorType.NONE);

    public static Error Validation(string code, string message, string? invalidField = null) =>
        new(code ?? "value.is.invalid", message, ErrorType.VALIDATION);
    public static Error NotFound(string code, string message) =>
        new(code ?? "record.not.found", message, ErrorType.NOT_FOUND);
    public static Error Failure(string code, string message) =>
        new(code ?? "failure", message, ErrorType.FAILURE);
    public static Error Conflict(string code, string message) =>
        new(code ?? "value.is.conflict", message, ErrorType.CONFLICT);
    public static Error Authorization(string code, string message) =>
        new(code ?? "authoruzation.error", message, ErrorType.AUTHORIZATION);
    public static Error Authentication(string code, string message) =>
        new(code ?? "authentication.error", message, ErrorType.AUTHENTICATION);

    public Failure ToFailure() => new([this]);

    // Разделяем код ошибки, сообщение ошибки и тип ошибки в строке
    public string Serialize()
    {
        return string.Join(SEPARATOR, Code, Message, Type);
    }

    public string GetMessage() => Message;

    // Собираем ошибку из строки с данными ошибки
    public static Error Deserialize(string serialized)
    {
        var parts = serialized.Split(SEPARATOR);

        if (parts.Length < 3 || !Enum.TryParse<ErrorType>(parts[2], out ErrorType type))
        {
            throw new ArgumentException("Invalid serialized format");
        }

        return new Error(parts[0], parts[1], type);
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ErrorType
{
    NONE,
    VALIDATION,
    NOT_FOUND,
    FAILURE,
    CONFLICT,
    AUTHORIZATION,
    AUTHENTICATION
}