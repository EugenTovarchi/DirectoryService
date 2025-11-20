using CSharpFunctionalExtensions;
using DirectoryService.SharedKernel;
using DirectoryService.SharedKernel.ValueObjects;
using DirectoryService.SharedKernel.ValueObjects.Ids;

namespace DirectoryService.Domain.Entities;

public sealed class Position : SoftDeletableEntity<PositionId>
{
    private Position(PositionId id) : base(id) { }
    private Position(
        PositionId positionId,
        Name name,
        Description? description
        ) : base(positionId)
    {
        Name = name;
        Description = description;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Name Name { get; private set; } = null!;
    public Description? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    //Локацию позиции если что будем доставить из Department? Чтобы не городить тут связи.
    private readonly List<DepartmentPosition> _departmentPositions = [];
    public IReadOnlyCollection<DepartmentPosition> DepartmentPositions => _departmentPositions.ToList();
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

        Name = Name.Create(newDescription).Value;
        return this;
    }

    public static Result<Position, Error> Create(
        Name name,
        Description description)
    {
        if (name is null)
            return Errors.General.ValueIsInvalid("name");

        if (description is null)
            return Errors.General.ValueIsInvalid("description");

        var positionId = PositionId.NewPositionId();
        var position = new Position( positionId, name, description);

        return position;
    }
}
