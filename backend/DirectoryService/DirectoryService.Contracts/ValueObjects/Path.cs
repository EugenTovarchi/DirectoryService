using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace DirectoryService.Contracts.ValueObjects;

public record Path
{
    public const int MAX_LENGTH = 500;
    private const char SEPARATOR = '.';
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

    public static Result<Path, Error> CreateForChild(Path parentPath, Identifier childIdentifier)
    {
        if (parentPath == null)
            return Errors.General.ValueIsRequired("parentPath");

        if (childIdentifier == null)
            return Errors.General.ValueIsRequired("childIdentifier");

        if (string.IsNullOrEmpty(parentPath.Value))
            return Errors.General.ValueIsInvalid("parentPath");

        string fullPath = string.IsNullOrEmpty(parentPath.Value)
            ? childIdentifier.Value
            : $"{parentPath.Value}{SEPARATOR}{childIdentifier.Value}";

        return Create(fullPath);
    }
}