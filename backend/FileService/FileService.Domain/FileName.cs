using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain;

/// <summary>
/// Хранит имя файла и автоматически извлекает расширение.
/// </summary>
public sealed record FileName
{
    public const int NAME_MAX_LENGTH = 500;
    public const int EXTENSION_MAX_LENGTH = 10;
    public const int VALUE_MAX_LENGTH = 500;

    public string Name { get; }
    public string Extension { get; }
    public string Value { get; }

    private FileName(string name, string extension)
    {
        Name = name;
        Extension = extension;
        Value = name + "." + extension;
    }

    private FileName() { }

    public static Result<FileName, Error> Create(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Errors.General.ValueIsInvalid("fileName");

        string normalized = fileName.Trim();
        if (normalized.Length > VALUE_MAX_LENGTH)
            return Errors.General.ValueIsTooLarge("fileName", VALUE_MAX_LENGTH);

        int extensionSeparatorIndex = normalized.LastIndexOf('.');
        if (extensionSeparatorIndex <= 0 || extensionSeparatorIndex == normalized.Length - 1)
            return Errors.General.ValueIsInvalid("extension");

        string name = normalized[..extensionSeparatorIndex];
        string extension = normalized[(extensionSeparatorIndex + 1)..].ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name) || name.Length > NAME_MAX_LENGTH)
            return Errors.General.ValueIsInvalid("fileName");

        if (extension.Length > EXTENSION_MAX_LENGTH)
            return Errors.General.ValueIsInvalid("extension");

        return new FileName(name, extension);
    }
}
