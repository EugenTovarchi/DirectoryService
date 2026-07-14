using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain;

/// <summary>
/// Хранит MIME-type и определяет категорию файла.
/// </summary>
public sealed record ContentType
{
    public const int VALUE_MAX_LENGTH = 100;

    public string Value { get; }
    public MediaType MediaType { get; }

    private ContentType(string value, MediaType mediaType)
    {
        Value = value;
        MediaType = mediaType;
    }

    public static Result<ContentType, Error> Create(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return Errors.General.ValueIsInvalid("contentType");

        string normalized = contentType.Trim();

        if (normalized.Length > VALUE_MAX_LENGTH)
            return Errors.General.ValueIsTooLarge("contentType", VALUE_MAX_LENGTH);

        MediaType category = normalized switch
        {
            _ when normalized.Contains("audio", StringComparison.InvariantCultureIgnoreCase) => MediaType.AUDIO,
            _ when normalized.Contains("video", StringComparison.InvariantCultureIgnoreCase) => MediaType.VIDEO,
            _ when normalized.Contains("document", StringComparison.InvariantCultureIgnoreCase) => MediaType.DOCUMENT,
            _ when normalized.Contains("image", StringComparison.InvariantCultureIgnoreCase) => MediaType.IMAGE,
            _ => MediaType.UNKNOWN
        };

        return new ContentType(normalized, category);
    }
}
