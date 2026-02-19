using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain;

/// <summary>
/// Хранит путь к файлу в S3-хранилище.
/// </summary>
public sealed record StorageKey
{
    // Название файла в хранилище, может быть Guid.
    public string Key { get; }

    // Папка в хранилище.
    public string Prefix { get; }

    // Путь файла.
    public string Location { get; }

    // Prefix + Key(папка + назв файла).
    public string Value { get; }

    // Состоит из Location + Prefix + Key.
    public string FullPath { get; }

    private StorageKey(string key, string prefix, string location)
    {
        Key = key;
        Prefix = prefix;
        Location = location;
        Value = string.IsNullOrEmpty(Prefix) ? Key : $"{prefix}/{Key}";
        FullPath = $"{Location}/{Value}";
    }

    public static Result<StorageKey, Error> Create(string key, string? prefix, string location)
    {
        if(string.IsNullOrWhiteSpace(location))
            return Errors.General.ValueIsInvalid("location");

        Result<string, Error> normilizedKeyResult = NormalizeSegment(key);
        if(normilizedKeyResult.IsFailure)
            return normilizedKeyResult.Error;

        Result<string, Error> normilizedPrefixResult = NormalizePrefix(prefix);
        if(normilizedPrefixResult.IsFailure)
            return normilizedKeyResult.Error;

        return new StorageKey(location.Trim(), normilizedPrefixResult.Value, normilizedKeyResult.Value);
    }

    private static Result<string, Error> NormalizePrefix(string? prefix)
    {
        if(string.IsNullOrWhiteSpace(prefix))
            return string.Empty;

        string [] parts = prefix.Trim().Replace('\\', '/').Split('/',  StringSplitOptions.RemoveEmptyEntries
                                                                       | StringSplitOptions.TrimEntries);
        List<string> normilizedParts = [];
        foreach (string part in parts)
        {
            Result<string, Error> normilizedPart = NormalizeSegment(part);
            if(normilizedPart.IsFailure)
                return normilizedPart;

            if(!string.IsNullOrEmpty(normilizedPart.Value))
                normilizedParts.Add(normilizedPart.Value);
        }

        return string.Join('/', normilizedParts);
    }

    private static Result<string, Error> NormalizeSegment(string? value)
    {
        if(string.IsNullOrWhiteSpace(value))
            return Errors.General.ValueIsInvalid("key");

        string trimmed = value.Trim();

        if (trimmed.Contains('/', StringComparison.Ordinal) || trimmed.Contains('\\', StringComparison.Ordinal))
            return Errors.General.ValueIsInvalid("key");

        return trimmed;
    }
}