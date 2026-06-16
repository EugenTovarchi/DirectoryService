using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace DirectoryService.Contracts.ValueObjects;

public record Path
{
    public const int MAX_LENGTH = 500;
    private const char SEPARATOR = '.';
    public string Value { get; } = string.Empty;
    private Path(string value)
    {
        Value = value;
    }

    private Path() { }

    public static Result<Path, Error> Create(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Errors.General.ValueIsInvalid("path");
        }

        string trimmed = value.Trim();

        if (trimmed.Length > MAX_LENGTH || !IsValidLtreePath(trimmed))
        {
            return Errors.General.ValueIsInvalid("path");
        }

        return new Path(trimmed);
    }

    public static Result<Path, Error> CreateForChild(Path parentPath, Identifier childIdentifier)
    {
        if (parentPath == null)
            return Errors.General.ValueIsRequired("parentPath");

        if (childIdentifier == null)
            return Errors.General.ValueIsRequired("childIdentifier");

        if (string.IsNullOrEmpty(parentPath.Value))
            return Errors.General.ValueIsInvalid("parentPath");

        string fullPath = string.IsNullOrEmpty(parentPath.Value)
            ? childIdentifier.Value
            : $"{parentPath.Value}{SEPARATOR}{childIdentifier.Value}";

        return Create(fullPath);
    }

    private static bool IsValidLtreePath(string value)
    {
        string[] segments = value.Split(SEPARATOR);

        return segments.All(segment => !string.IsNullOrEmpty(segment) && IsValidLtreeLabel(segment));
    }

    private static bool IsValidLtreeLabel(string value)
    {
        foreach (char character in value)
        {
            bool isAsciiLetter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
            bool isDigit = character is >= '0' and <= '9';

            if (!isAsciiLetter && !isDigit && character != '_')
                return false;
        }

        return true;
    }
}
