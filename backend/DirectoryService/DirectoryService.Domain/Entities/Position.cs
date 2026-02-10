using CSharpFunctionalExtensions;
using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Contracts.ValueObjects.Ids;
using SharedService.SharedKernel;

namespace DirectoryService.Domain.Entities;

public sealed class Position : SoftDeletableEntity<PositionId>
{
    private Position(PositionId id)
        : base(id) { }
    private Position(
        PositionId positionId,
        Name name,
        Description? description)
        : base(positionId)
    {
        Name = name;
        Description = description ?? null;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Name Name { get; private set; } = null!;
    public Description? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<DepartmentPosition> _departmentPositions = [];
    public IReadOnlyCollection<DepartmentPosition> DepartmentPositions => _departmentPositions.ToList();

    public override void Delete()
    {
        base.Delete();
        UpdatedAt = DateTime.UtcNow;
    }

    public override void Restore()
    {
        base.Restore();
        UpdatedAt = DateTime.UtcNow;
    }

    public Result<Position, Error> UpdateName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return Errors.General.ValueIsEmptyOrWhiteSpace("position name");

        if (newName.Length > 100)
            return Errors.General.ValueIsInvalid("newName");

        Name = Name.Create(newName).Value;
        return this;
    }

    public Result<Position, Error> UpdateDescription(string newDescription)
    {
        if (string.IsNullOrWhiteSpace(newDescription))
            return Errors.General.ValueIsEmptyOrWhiteSpace("position description");

        if (newDescription.Length > 100)
            return Errors.General.ValueIsInvalid("newDescription");

        Description = Description.Create(newDescription).Value;
        return this;
    }

    public static Result<Position, Error> CreateWithDescription(
        Name name,
        Description description)
    {
        if (name is null)
            return Errors.General.ValueIsInvalid("name");

        if (description is null)
            return Errors.General.ValueIsInvalid("description");

        var positionId = PositionId.NewPositionId();
        var position = new Position(positionId, name, description);

        return position;
    }

    public static Result<Position, Error> CreateWithoutDescription(Name name)
    {
        if (name is null)
            return Errors.General.ValueIsInvalid("name");

        var positionId = PositionId.NewPositionId();
        var position = new Position(positionId, name, null);

        return position;
    }

    public UnitResult<Error> AddDepartmentPosition(DepartmentId departmentId)
    {
        if (departmentId == null || departmentId == DepartmentId.EmptyDepartmentId())
            return Errors.General.ValueIsInvalid("locationId");

        if (_departmentPositions.Any(dl => dl.DepartmentId == departmentId))
            return Errors.General.Duplicate("departmentId");

        var departmentPositionResult = DepartmentPosition.Create(Id, departmentId);
        if (departmentPositionResult.IsFailure)
            return departmentPositionResult.Error;

        _departmentPositions.Add(departmentPositionResult.Value);
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    public UnitResult<Error> RemoveDepartmentPosition(DepartmentId departmentId)
    {
        if (departmentId == null || departmentId == DepartmentId.EmptyDepartmentId())
            return Errors.General.ValueIsInvalid("departmentId");

        var departmentPosition = _departmentPositions.FirstOrDefault(dl => dl.DepartmentId == departmentId);
        if (departmentPosition == null)
            return Errors.General.NotFound(departmentId.Value);

        _departmentPositions.Remove(departmentPosition);
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }
}
