using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Domain.Users;

public sealed record Email
{
    public const int MAX_LENGTH = 320;

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Email, Error> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Errors.General.ValueIsEmptyOrWhiteSpace("email");

        string normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MAX_LENGTH || !normalized.Contains('@', StringComparison.Ordinal))
            return Errors.General.ValueIsInvalid("email");

        return new Email(normalized);
    }
}
