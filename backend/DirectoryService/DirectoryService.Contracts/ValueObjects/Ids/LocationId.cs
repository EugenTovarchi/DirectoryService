namespace DirectoryService.Contracts.ValueObjects.Ids;

public sealed class LocationId : ValueObject, IComparable<LocationId>, IEquatable<LocationId>
{
    private LocationId() { }
    private LocationId(Guid value) => Value = value;

    public Guid Value { get; }

    public static LocationId NewLocationId() => new(Guid.NewGuid());

    public static LocationId EmptyLocationId() => new(Guid.Empty);

    public static LocationId Create(Guid id) => new(id);

    public static bool operator ==(LocationId? left, LocationId? right)
    {
        return left is null ? right is null : left.Equals(right);
    }

    public static bool operator !=(LocationId? left, LocationId? right)
    {
        return !(left == right);
    }

    public static bool operator <(LocationId? left, LocationId? right)
    {
        return left is null ? right is not null : left.CompareTo(right) < 0;
    }

    public static bool operator <=(LocationId? left, LocationId? right)
    {
        return left is null || left.CompareTo(right) <= 0;
    }

    public static bool operator >(LocationId? left, LocationId? right)
    {
        return left is not null && left.CompareTo(right) > 0;
    }

    public static bool operator >=(LocationId? left, LocationId? right)
    {
        return left is null ? right is null : left.CompareTo(right) >= 0;
    }

    public static implicit operator Guid(LocationId locationId)
    {
        ArgumentNullException.ThrowIfNull(locationId);
        return locationId.Value;
    }

    public int CompareTo(LocationId? other)
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
        return Equals(obj as LocationId);
    }

    public bool Equals(LocationId? other)
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
