namespace DirectoryService.SharedKernel.ValueObjects.Ids;

public class DepartmentId : ValueObject, IComparable<DepartmentId>
{
    private DepartmentId()
    {

    }
    private DepartmentId(Guid value) => Value = value;

    public Guid Value { get; }

    public static DepartmentId NewDepartmentId() => new(Guid.NewGuid());

    public static DepartmentId EmptyDepartmentId() => new(Guid.Empty);

    public static DepartmentId Create(Guid id) => new(id);

    public static implicit operator Guid(DepartmentId departmentId)
    {
        ArgumentNullException.ThrowIfNull(departmentId);
        return departmentId.Value;
    }
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public int CompareTo(DepartmentId? other)
    {
        if (other is null)
            return 1; // Все ненулевые объекты больше null

        return Value.CompareTo(other.Value);
    }
}
