using CSharpFunctionalExtensions;
using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.SharedKernel;
using Path = DirectoryService.Contracts.ValueObjects.Path;

namespace DirectoryService.Domain.Entities;

public sealed class Department : SoftDeletableEntity<DepartmentId>
{
    private Department(DepartmentId id)
        : base(id) { }
    private Department(
        DepartmentId departmentId,
        Name name,
        Identifier identifier,
        Path path,
        short depth,
        DepartmentId? parentId)
        : base(departmentId)
    {
        Name = name;
        Identifier = identifier;
        Path = path;
        Depth = depth;
        ParentId = parentId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Name Name { get; private set; } = null!;
    public Identifier Identifier { get; private set; } = null!;
    public DepartmentId? ParentId { get; private set; }
    public Path Path { get; private set; } = null!;
    public short Depth { get; private set; }

    private readonly List<Department> _childrenDepartment = [];
    private readonly List<DepartmentLocation> _departmentLocations = [];
    private readonly List<DepartmentPosition> _departmentPositions = [];

    public IReadOnlyCollection<Department> ChildrenDepartment => [.. _childrenDepartment];
    public IReadOnlyCollection<DepartmentLocation> DepartmentLocations => [.. _departmentLocations];
    public IReadOnlyCollection<DepartmentPosition> DepartmentPositions => [.. _departmentPositions];

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

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

    public UnitResult<Error> AddChildren(Department children)
    {
        if (children is null)
            return Errors.General.NotFoundEntity("children");

        _childrenDepartment.Add(children);
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    public static Result<Department, Error> CreateRoot(Name name, Identifier identifier)
    {
        var pathResult = Path.Create(identifier.Value);
        if (pathResult.IsFailure)
            return pathResult.Error;

        return new Department(
            DepartmentId.NewDepartmentId(),
            name,
            identifier,
            pathResult.Value,
            depth: 0,
            parentId: null);
    }

    public UnitResult<Error> MoveTo(Department? newParent)
    {
        if (newParent == null)
        {
            var pathResult = Path.Create(Identifier.Value);
            if (pathResult.IsFailure)
                return pathResult.Error;

            Path = pathResult.Value;
            Depth = 0;
            ParentId = null;
            UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var pathResult = Path.CreateForChild(newParent.Path, Identifier);
            if (pathResult.IsFailure)
                return pathResult.Error;

            Path = pathResult.Value;
            Depth = (short)(newParent.Depth + 1);
            ParentId = newParent.Id;
            UpdatedAt = DateTime.UtcNow;
        }

        return Result.Success<Error>();
    }

    public static Result<Department, Error> CreateChild(
        Name name,
        Identifier identifier,
        Department parent)
    {
        if (parent == null)
            return Errors.General.NotFoundEntity("parent");

        var pathResult = Path.CreateForChild(parent.Path, identifier);
        if (pathResult.IsFailure)
            return pathResult.Error;

        return new Department(
            DepartmentId.NewDepartmentId(),
            name,
            identifier,
            pathResult.Value,
            depth: (short)(parent.Depth + 1),
            parentId: parent.Id);
    }

    public UnitResult<Error> MoveAsChildDepartment(Department parent)
    {
        if (parent == null)
            return Errors.General.NotFoundEntity("parent");

        var pathResult = Path.CreateForChild(parent.Path, Identifier);
        if (pathResult.IsFailure)
            return pathResult.Error;

        Path = pathResult.Value;
        Depth = (short)(parent.Depth + 1);
        ParentId = parent.Id;
        UpdatedAt = DateTime.UtcNow;

        parent.AddChildren(this);

        return Result.Success<Error>();
    }

    public UnitResult<Error> AddLocation(LocationId locationId)
    {
        if (locationId == null || locationId == LocationId.EmptyLocationId())
            return Errors.General.ValueIsInvalid("locationId");

        if (_departmentLocations.Any(dl => dl.LocationId == locationId))
            return Errors.General.Duplicate("locationId");

        var departmentLocationResult = DepartmentLocation.Create(locationId, Id);
        if (departmentLocationResult.IsFailure)
            return departmentLocationResult.Error;

        _departmentLocations.Add(departmentLocationResult.Value);
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    public UnitResult<Error> AddDepartmentLocation(IEnumerable<DepartmentLocation> newLocations)
    {
        if (newLocations == null)
            return Errors.General.ValueIsInvalid("newLocations");

        _departmentLocations.AddRange(newLocations.ToList());

        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    public UnitResult<Error> RemoveLocation(LocationId locationId)
    {
        if (locationId == null || locationId == LocationId.EmptyLocationId())
            return Errors.General.ValueIsInvalid("locationId");

        var departmentLocation = _departmentLocations.FirstOrDefault(dl => dl.LocationId == locationId);
        if (departmentLocation == null)
            return Errors.General.NotFound(locationId);

        _departmentLocations.Remove(departmentLocation);
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    public UnitResult<Error> AddPosition(PositionId positionId, DepartmentId departmentId)
    {
        if (positionId == null || positionId == PositionId.EmptyPositionId())
            return Errors.General.ValueIsInvalid("locationId");

        if (_departmentPositions.Any(dl => dl.PositionId == positionId))
            return Errors.General.Duplicate("positionId");

        var departmentPositionResult = DepartmentPosition.Create(positionId, departmentId);
        if (departmentPositionResult.IsFailure)
            return departmentPositionResult.Error;

        _departmentPositions.Add(departmentPositionResult.Value);
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    public UnitResult<Error> RemovePosition(PositionId positionId)
    {
        if (positionId == null || positionId == PositionId.EmptyPositionId())
            return Errors.General.ValueIsInvalid("positionId");

        var departmentPosition = _departmentPositions.FirstOrDefault(dl => dl.PositionId == positionId);
        if (departmentPosition == null)
            return Errors.General.NotFound(positionId.Value);

        _departmentPositions.Remove(departmentPosition);
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    internal UnitResult<Error> UpdateBasicInfo(
        Name name,
        Identifier identifier,
        Path path,
        short depth)
    {
        Name = name;
        Identifier = identifier;
        Path = path;
        Depth = depth;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    internal UnitResult<Error> SetParentDepartment(DepartmentId parentId)
    {
        ParentId = parentId;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }
}
