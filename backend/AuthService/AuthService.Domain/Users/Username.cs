using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Domain.Users;

public sealed record Username
{
    public const int MIN_LENGTH = 3;
    public const int MAX_LENGTH = 50;

    private Username(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Username, Error> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Errors.General.ValueIsEmptyOrWhiteSpace("username");

        string normalized = value.Trim();

        if (normalized.Length < MIN_LENGTH || normalized.Length > MAX_LENGTH)
            return Errors.General.ValueIsInvalid("username");

        return new Username(normalized);
    }
}
