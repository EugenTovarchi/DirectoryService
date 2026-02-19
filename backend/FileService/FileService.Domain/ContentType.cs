using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain;

/// <summary>
/// Хранит MIME-type и определяет категорию файла.
/// </summary>
public sealed record ContentType
{
    public string Value { get; }
    public MediaType MediaType { get; }

    private ContentType(string value, MediaType mediaType)
    {
        Value = value;
        MediaType = mediaType;
    }

    public static Result<ContentType, Error> Create(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return Errors.General.ValueIsInvalid("contentType");

        MediaType category = contentType switch
        {
            _ when contentType.Contains("audio", StringComparison.InvariantCultureIgnoreCase) => MediaType.AUDIO,
            _ when contentType.Contains("video", StringComparison.InvariantCultureIgnoreCase) => MediaType.VIDEO,
            _ when contentType.Contains("document", StringComparison.InvariantCultureIgnoreCase) => MediaType.DOCUMENT,
            _ when contentType.Contains("image", StringComparison.InvariantCultureIgnoreCase) => MediaType.IMAGE,
            _ => MediaType.UNKNOWN
        };

        return new ContentType(contentType, category);
    }
}