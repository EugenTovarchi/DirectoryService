using CSharpFunctionalExtensions; 

namespace DirectoryService.SharedKernel.ValueObjects;

public record Name
{
    public const int MAX_LENGTH = 120;
    public string Value { get; } = string.Empty;
    private Name(string value)
    {
        Value = value;
    }

    private Name() { }

    public static Result<Name, Error> Create(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MAX_LENGTH && value.Length < 3)
        {
            return Errors.General.ValueIsInvalid("name");
        }

        return new Name(value);
    }
}
