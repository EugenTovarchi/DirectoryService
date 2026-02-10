namespace DirectoryService.SharedKernel.ValueObjects.Ids;

public class PositionId : ValueObject, IComparable<PositionId>
{
    private PositionId()
    {

    }
    private PositionId(Guid value) => Value = value;

    public Guid Value { get; }

    public static PositionId NewPositionId() => new(Guid.NewGuid());

    public static PositionId EmptyPositionId() => new(Guid.Empty);

    public static PositionId Create(Guid id) => new(id);

    public static implicit operator Guid(PositionId positionId)
    {
        ArgumentNullException.ThrowIfNull(positionId);
        return positionId.Value;
    }
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public int CompareTo(PositionId? other)
    {
        if (other is null)
            return 1; // Все ненулевые объекты больше null

        return Value.CompareTo(other.Value);
    }
}
