using CSharpFunctionalExtensions;
using System.Text.RegularExpressions;

namespace DirectoryService.SharedKernel.ValueObjects;

public record Description
{
    public const int MAX_LENGTH = 1000;
    public string Value { get; } = string.Empty;
    private Description(string value)
    {
        Value = value;
    }

    private Description() { }

    public static Result<Description, Error> Create (string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MAX_LENGTH)
        {
            return Errors.General.ValueIsInvalid("description");
        }

        string normilized = Regex.Replace(value.Trim(), @"\s+", " ");

        return new Description(normilized);
    }
}
