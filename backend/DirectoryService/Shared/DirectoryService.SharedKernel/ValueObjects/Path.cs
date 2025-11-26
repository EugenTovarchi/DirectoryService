using CSharpFunctionalExtensions;
using System.Text.RegularExpressions;

namespace DirectoryService.SharedKernel.ValueObjects;

public  record Path
{
    public const int MAX_LENGTH = 50;
    public string Value { get; } = string.Empty;
    private Path(string value)
    {
        Value = value;
    }

    private Path() { }

    public static Result<Path, Error> Create(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MAX_LENGTH)
        {
            return Errors.General.ValueIsInvalid("path");
        }

        string normilized = Regex.Replace(value.Trim(), @"\s+", " ");

        return new Path(normilized);
    }
}
