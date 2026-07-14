using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace DirectoryService.Contracts.ValueObjects;

public sealed record Name
{
    public const int MAX_LENGTH = 120;
    public const int MIN_LENGTH = 3;
    public string Value { get; } = string.Empty;
    private Name(string value)
    {
        Value = value;
    }

    private Name() { }

    public static Result<Name, Error> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Errors.General.ValueIsInvalid("name");
        }

        string normalized = Regex.Replace(value.Trim(), @"\s+", " ",
            RegexOptions.Compiled,  TimeSpan.FromMilliseconds(100));

        if (normalized.Length > MAX_LENGTH || normalized.Length < MIN_LENGTH)
        {
            return Errors.General.ValueIsInvalid("name");
        }

        return new Name(normalized);
    }
}
