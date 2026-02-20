using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain;

/// <summary>
/// Хранит имя файла и автоматически извлекает расширение.
/// </summary>
public sealed record FileName
{
    public string Value { get; }
    public string Extension { get; }

    private FileName(string value, string extension)
    {
        Value = value;
        Extension = extension;
    }

    public static Result<FileName, Error> Create(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Errors.General.ValueIsInvalid("fileName");

        int lastDot = fileName.LastIndexOf('.');

        // Если точек нет вообще или точка явл последним символом(readme.).
        if (lastDot == -1 || lastDot == fileName.Length - 1)
            return Errors.General.ValueIsInvalid("extension");

        // Вырезаем все, что после последней точки.
        string extension = fileName[(lastDot + 1)..].ToLowerInvariant();
        return new FileName(fileName, extension);
    }
}