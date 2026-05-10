using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace DirectoryService.Contracts.ValueObjects;

public record Identifier
{
    public const int MAX_LENGTH = 150;
    public const int MIN_LENGTH = 3;

    public string Value { get; } = string.Empty;
    private Identifier(string value)
    {
        Value = value;
    }

    private Identifier() { }

    public static Result<Identifier, Error> Create(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Errors.General.ValueIsInvalid("identifier");
        }

        string trimmed = value.Trim();

        if (trimmed.Length > MAX_LENGTH || trimmed.Length < MIN_LENGTH || !IsValidLtreeLabel(trimmed))
        {
            return Errors.General.ValueIsInvalid("identifier");
        }

        return new Identifier(trimmed);
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
