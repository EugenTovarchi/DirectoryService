namespace DirectoryService.Contracts.ValueObjects.Ids;

public class DepartmentId : ValueObject, IComparable<DepartmentId>
{
    public Guid Value { get; }

    private DepartmentId()
    {
    }

    private DepartmentId(Guid value) => Value = value;

    public static DepartmentId NewDepartmentId() => new(Guid.NewGuid());

    public static DepartmentId EmptyDepartmentId() => new(Guid.Empty);

    public static DepartmentId Create(Guid id) => new(id);

    public static implicit operator Guid(DepartmentId departmentId)
    {
        ArgumentNullException.ThrowIfNull(departmentId);
        return departmentId.Value;
    }

    public int CompareTo(DepartmentId? other)
    {
        return other is null ? 1 : // Все ненулевые объекты больше null
            Value.CompareTo(other.Value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}