using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace DirectoryService.Contracts.ValueObjects;

public record TimeZone
{
    public const int MAX_LENGTH = 50;
    private TimeZone() { } // EF Core
    private TimeZone(string value)
    {
        Value = value;
    }

    public string Value { get; } = string.Empty;

    public static Result<TimeZone, Error> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Errors.General.ValueIsRequired("timeZone");

        if (value.Length > MAX_LENGTH)
            return Errors.General.ValueIsTooLarge("timeZone", MAX_LENGTH);

        if (!IsValidTimeZoneFormat(value))
            return Errors.General.ValueIsInvalid("timeZone");

        return new TimeZone(value.Trim());
    }

    private static bool IsValidTimeZoneFormat(string value)
    {
        // IANA формат: Continent/City или Region/City
        var parts = value.Split('/');
        return parts.Length >= 2 &&
               !string.IsNullOrWhiteSpace(parts[0]) &&
               !string.IsNullOrWhiteSpace(parts[1]);
    }

}