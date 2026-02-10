using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace DirectoryService.Contracts.ValueObjects;

public record Name
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
        if (string.IsNullOrEmpty(value) || value.Length > MAX_LENGTH || value.Length < MIN_LENGTH)
        {
            return Errors.General.ValueIsInvalid("name");
        }

        string normilized = Regex.Replace(value.Trim(), @"\s+", " ");

        return new Name(normilized);
    }
}
