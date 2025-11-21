using CSharpFunctionalExtensions;

namespace DirectoryService.SharedKernel.ValueObjects;

public record Identifier
{
    public const int MAX_LENGTH = 150;
    public string Value { get; } = string.Empty;
    private Identifier(string value)
    {
        Value = value;
    }

    private Identifier() { }

    public static Result<Identifier, Error> Create(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MAX_LENGTH && value.Length < 3)
        {
            return Errors.General.ValueIsInvalid("identifier");
        }

        return new Identifier(value);
    }
}
