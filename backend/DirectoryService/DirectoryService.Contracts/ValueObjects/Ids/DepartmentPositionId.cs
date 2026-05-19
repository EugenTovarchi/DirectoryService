namespace DirectoryService.Contracts.ValueObjects.Ids;

public sealed class DepartmentPositionId : ValueObject, IComparable<DepartmentPositionId>, IEquatable<DepartmentPositionId>
{
    public Guid Value { get; }

    private DepartmentPositionId() { }

    private DepartmentPositionId(Guid value) => Value = value;

    public static DepartmentPositionId NewDepartmentPositionId() => new(Guid.NewGuid());

    public static DepartmentPositionId EmptyDepartmentPositionId() => new(Guid.Empty);

    public static DepartmentPositionId Create(Guid id) => new(id);

    public static bool operator ==(DepartmentPositionId? left, DepartmentPositionId? right)
    {
        return left is null ? right is null : left.Equals(right);
    }

    public static bool operator !=(DepartmentPositionId? left, DepartmentPositionId? right)
    {
        return !(left == right);
    }

    public static bool operator <(DepartmentPositionId? left, DepartmentPositionId? right)
    {
        return left is null ? right is not null : left.CompareTo(right) < 0;
    }

    public static bool operator <=(DepartmentPositionId? left, DepartmentPositionId? right)
    {
        return left is null || left.CompareTo(right) <= 0;
    }

    public static bool operator >(DepartmentPositionId? left, DepartmentPositionId? right)
    {
        return left is not null && left.CompareTo(right) > 0;
    }

    public static bool operator >=(DepartmentPositionId? left, DepartmentPositionId? right)
    {
        return left is null ? right is null : left.CompareTo(right) >= 0;
    }

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

    public bool Equals(DepartmentPositionId? other)
    {
        if (other is null)
            return false;

        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as DepartmentPositionId);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
