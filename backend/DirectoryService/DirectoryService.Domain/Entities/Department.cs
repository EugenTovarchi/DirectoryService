using CSharpFunctionalExtensions;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects;
using DirectoryService.SharedKernel.ValueObjects.Ids;
using Path = DirectoryService.SharedKernel.ValueObjects.Path;

namespace DirectoryService.Domain.Entities;

public sealed class Department : SoftDeletableEntity<DepartmentId>
{
    private Department(DepartmentId id) : base(id) { }
    private  Department(
        DepartmentId departmentId,
        Name name,
        Identifier identifier,
        Path path,
        short depth,
        Guid? parentId
        ): base (departmentId)
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
    public Guid? ParentId { get; private set; }
    public Path Path { get; private set; } = null!;
    public short Depth { get; private set; }

    private readonly List<DepartmentLocation> _departmentLocations = [];
    private readonly List<DepartmentPosition> _departmentPositions = [];
    public IReadOnlyCollection<DepartmentLocation> DepartmentOffices => [.. _departmentLocations];
    public IReadOnlyCollection<DepartmentPosition> DepartmentPositions => [.. _departmentPositions];

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public override void Delete()
    {
        base.Delete();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        base.Restor();
        UpdatedAt = DateTime.UtcNow;
    }

    internal UnitResult<Error> UpdateBasicInfo(
        Name name,
        Identifier identifier,
        Path path,
        short depth,
        DateTime updateTime)
    {
        Name = name;
        Identifier = identifier;
        Path = path;
        Depth = depth;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    internal UnitResult<Error> SetParentDepartment (Guid parentId)
    {
        ParentId = parentId;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }

    internal static Result<Department,Error> Create(
        Name name,
        Identifier identifier,
        Path path,
        short depth,
        Guid? parentId = null)
    {
        if (depth < 0)
            return Errors.General.ValueIsInvalid("depth");

        var departmentId = DepartmentId.NewDepartmentId();
        var createdAt = DateTime.UtcNow;

        var department = new Department(
             departmentId,
             name,
             identifier,
             path,
             depth,
             parentId)
        {
            Name = name,
            Identifier = identifier,
            Path = path,
            Depth = depth,
            ParentId = parentId,
            CreatedAt = createdAt
        };

        return department;
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

    public UnitResult<Error> RemoveLocation(LocationId locationId)
    {
        if (locationId == null || locationId == LocationId.EmptyLocationId())
            return Errors.General.ValueIsInvalid(nameof(locationId));

        var departmentLocation = _departmentLocations.FirstOrDefault(dl => dl.LocationId == locationId);
        if (departmentLocation == null)
            return Errors.General.NotFound(locationId);

        _departmentLocations.Remove(departmentLocation);
        UpdatedAt = DateTime.UtcNow;

        return Result.Success<Error>();
    }
    public UnitResult<Error> AddPosition(PositionId positionId)
    {
        if (positionId == null || positionId == PositionId.EmptyPositionId())
            return Errors.General.ValueIsInvalid("locationId");

        if (_departmentPositions.Any(dl => dl.PositionId == positionId))
            return Errors.General.Duplicate("positionId");

        var departmentPositionResult = DepartmentPosition.Create(positionId, Id);
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
}
