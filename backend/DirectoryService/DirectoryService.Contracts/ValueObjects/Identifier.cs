using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using DirectoryService.SharedKernel;

namespace DirectoryService.Contracts.ValueObjects;

public record Identifier
{
    public const int MAX_LENGTH = 150;
    public const int MIN_LENGTH = 3;

    public string Value { get; } = string.Empty;
    private Identifier(string value)
    {
        Value = value;
    }

    private Identifier() { }

    public static Result<Identifier, Error> Create(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MAX_LENGTH || value.Length < MIN_LENGTH)
        {
            return Errors.General.ValueIsInvalid("identifier");
        }

        string normilized = Regex.Replace(value.Trim(), @"\s+", " ");

        return new Identifier(normilized);
    }
}
