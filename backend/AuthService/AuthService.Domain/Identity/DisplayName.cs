using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace AuthService.Domain.Identity;

/// <summary>
/// Optional имя для UI и JWT claim `name`; это не юридическое ФИО пользователя.
/// </summary>
public sealed record DisplayName
{
    public const int MIN_LENGTH = IdentityFieldLimits.DISPLAY_NAME_MIN_LENGTH;
    public const int MAX_LENGTH = IdentityFieldLimits.DISPLAY_NAME_MAX_LENGTH;

    private DisplayName(string value)
    {
        Value = value;
    }

    private DisplayName()
    {
    }

    public string Value { get; } = string.Empty;

    public static Result<DisplayName, Error> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Errors.General.ValueIsInvalid("displayName");

        string normalized = Regex.Replace(
            value.Trim(),
            @"\s+",
            " ",
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        if (normalized.Length is < MIN_LENGTH or > MAX_LENGTH)
            return Errors.General.ValueIsInvalid("displayName");

        return new DisplayName(normalized);
    }
}
