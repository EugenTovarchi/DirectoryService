using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace DirectoryService.Contracts.ValueObjects;

public record Title
{
    public const int MAX_LENGTH = 200;
    public string Value { get; } = string.Empty;
    private Title(string value)
    {
        Value = value;
    }

    private Title() { }

    public static Result<Title, Error> Create(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MAX_LENGTH)
        {
            return Errors.General.ValueIsInvalid("title");
        }

        string normilized = Regex.Replace(value.Trim(), @"\s+", " ");

        return new Title(normilized);
    }
}