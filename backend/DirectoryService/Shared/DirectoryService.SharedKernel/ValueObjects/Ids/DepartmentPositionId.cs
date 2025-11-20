namespace DirectoryService.SharedKernel.ValueObjects.Ids;

public class DepartmentPositionId : ValueObject, IComparable<DepartmentPositionId>
{
    private DepartmentPositionId() { }
    private DepartmentPositionId(Guid value) => Value = value;

    public Guid Value { get; }

    public static DepartmentPositionId NewDepartmentPositionId() => new(Guid.NewGuid());

    public static DepartmentPositionId EmptyDepartmentPositionId() => new(Guid.Empty);

    public static DepartmentPositionId Create(Guid id) => new(id);

    public static implicit operator Guid(DepartmentPositionId locationId)
    {
        ArgumentNullException.ThrowIfNull(locationId);
        return locationId.Value;
    }
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public int CompareTo(DepartmentPositionId? other)
    {
        if (other is null)
            return 1; // Все ненулевые объекты больше null

        return Value.CompareTo(other.Value);
    }
}
