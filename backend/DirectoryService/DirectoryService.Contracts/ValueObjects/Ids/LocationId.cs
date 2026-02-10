namespace DirectoryService.Contracts.ValueObjects.Ids;

public class LocationId : ValueObject, IComparable<LocationId>
{
    private LocationId() { }
    private LocationId(Guid value) => Value = value;

    public Guid Value { get; }

    public static LocationId NewLocationId() => new(Guid.NewGuid());

    public static LocationId EmptyLocationId() => new(Guid.Empty);

    public static LocationId Create(Guid id) => new(id);

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
}
