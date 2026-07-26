using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace DirectoryService.Contracts.ValueObjects;

public sealed record Description
{
    public const int MAX_LENGTH = 1000;
    public string Value { get; } = string.Empty;
    private Description(string value)
    {
        Value = value;
    }

    private Description() { }

    public static Result<Description, Error> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Errors.General.ValueIsInvalid("description");
        }

        string normalized = Regex.Replace(value.Trim(), @"\s+", " ",
            RegexOptions.Compiled,  TimeSpan.FromMilliseconds(100));

        if (normalized.Length > MAX_LENGTH)
        {
            return Errors.General.ValueIsInvalid("description");
        }

        return new Description(normalized);
    }
}
