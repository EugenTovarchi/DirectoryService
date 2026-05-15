using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Domain.Users;

public sealed record PasswordHash
{
    public const int MIN_LENGTH = 20;
    public const int MAX_LENGTH = 500;

    private PasswordHash(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<PasswordHash, Error> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Errors.General.ValueIsEmptyOrWhiteSpace("passwordHash");

        string trimmed = value.Trim();

        if (trimmed.Length < MIN_LENGTH || trimmed.Length > MAX_LENGTH)
            return Errors.General.ValueIsInvalid("passwordHash");

        return new PasswordHash(trimmed);
    }
}
