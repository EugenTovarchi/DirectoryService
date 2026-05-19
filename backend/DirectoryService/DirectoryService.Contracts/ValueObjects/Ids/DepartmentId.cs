namespace DirectoryService.Contracts.ValueObjects.Ids;

public sealed class DepartmentId : ValueObject, IComparable<DepartmentId>, IEquatable<DepartmentId>
{
    public Guid Value { get; }

    private DepartmentId()
    {
    }

    private DepartmentId(Guid value) => Value = value;

    public static DepartmentId NewDepartmentId() => new(Guid.NewGuid());

    public static DepartmentId EmptyDepartmentId() => new(Guid.Empty);

    public static DepartmentId Create(Guid id) => new(id);

    public static bool operator ==(DepartmentId? left, DepartmentId? right)
    {
        return left is null ? right is null : left.Equals(right);
    }

    public static bool operator !=(DepartmentId? left, DepartmentId? right)
    {
        return !(left == right);
    }

    public static bool operator <(DepartmentId? left, DepartmentId? right)
    {
        return left is null ? right is not null : left.CompareTo(right) < 0;
    }

    public static bool operator <=(DepartmentId? left, DepartmentId? right)
    {
        return left is null || left.CompareTo(right) <= 0;
    }

    public static bool operator >(DepartmentId? left, DepartmentId? right)
    {
        return left is not null && left.CompareTo(right) > 0;
    }

    public static bool operator >=(DepartmentId? left, DepartmentId? right)
    {
        return left is null ? right is null : left.CompareTo(right) >= 0;
    }

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

    public override bool Equals(object? obj)
    {
        return Equals(obj as DepartmentId);
    }

    public bool Equals(DepartmentId? other)
    {
        if (other is null)
            return false;

        return Value == other.Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}
