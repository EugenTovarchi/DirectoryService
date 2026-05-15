using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Domain.Identity;

/// <summary>
/// Валидирует username до передачи значения в ASP.NET Core Identity.
/// </summary>
public sealed record Username
{
    public const int MIN_LENGTH = IdentityFieldLimits.USERNAME_MIN_LENGTH;
    public const int MAX_LENGTH = IdentityFieldLimits.USERNAME_MAX_LENGTH;

    private Username(string value)
    {
        Value = value;
    }

    private Username()
    {
    }

    public string Value { get; } = string.Empty;

    public static Result<Username, Error> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Errors.General.ValueIsEmptyOrWhiteSpace("username");

        string normalized = value.Trim();

        if (normalized.Length is < MIN_LENGTH or > MAX_LENGTH)
            return Errors.General.ValueIsInvalid("username");

        return new Username(normalized);
    }
}
