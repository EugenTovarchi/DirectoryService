namespace DirectoryService.SharedKernel.ValueObjects.Ids;

public class DepartmentPositionId : ValueObject, IComparable<DepartmentPositionId>
{
    public Guid Value { get; }

    private DepartmentPositionId() { }

    private DepartmentPositionId(Guid value) => Value = value;

    public static DepartmentPositionId NewDepartmentPositionId() => new(Guid.NewGuid());

    public static DepartmentPositionId EmptyDepartmentPositionId() => new(Guid.Empty);

    public static DepartmentPositionId Create(Guid id) => new(id);

    public static implicit operator Guid(DepartmentPositionId locationId)
    {
        ArgumentNullException.ThrowIfNull(locationId);
        return locationId.Value;
    }

    public int CompareTo(DepartmentPositionId? other)
    {
        return other is null ? 1 : // Все ненулевые объекты больше null
            Value.CompareTo(other.Value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
