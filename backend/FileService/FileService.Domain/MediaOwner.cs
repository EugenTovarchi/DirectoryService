using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace FileService.Domain;

/// <summary>
/// Типобезопасная связь с владельцем файла из другого сервиса.
/// </summary>
public sealed record MediaOwner
{
    // EfCore.
    public MediaOwner()
    {
    }

    private MediaOwner(string context, Guid entityId)
    {
        Context = context;
        EntityId = entityId;
    }

    public static readonly HashSet<string> AllowedContext =
    [
        "department",
        "location",
        "position"
    ];

    public string Context { get; }
    public Guid EntityId { get; }

    public static Result<MediaOwner, Error> Create(string context, Guid entityId)
    {
        if (string.IsNullOrWhiteSpace(context))
            return Errors.General.ValueIsInvalid("context");

        if (context.Length > 50)
            return Errors.General.ValueIsTooLarge("context", 50);

        string normilizedContext = context.Trim().ToLowerInvariant();
        if (!AllowedContext.Contains(normilizedContext))
            return Errors.Validation.RecordIsInvalid("context");

        if (entityId == Guid.Empty)
            return Errors.General.ValueIsInvalid("entityId");

        return new MediaOwner(normilizedContext, entityId);
    }

    public static Result<MediaOwner, Error> ForDepartment(Guid departmentId) => Create("department", departmentId);
    public static Result<MediaOwner, Error> ForLocation(Guid locationId) => Create("location", locationId);
    public static Result<MediaOwner, Error> ForPosition(Guid positionId) => Create("position", positionId);
}