namespace DirectoryService.Contracts.ValueObjects.Ids;

public sealed class PositionId : ValueObject, IComparable<PositionId>, IEquatable<PositionId>
{
    public Guid Value { get; }

    private PositionId()
    {
    }

    private PositionId(Guid value) => Value = value;

    public static PositionId NewPositionId() => new(Guid.NewGuid());

    public static PositionId EmptyPositionId() => new(Guid.Empty);

    public static PositionId Create(Guid id) => new(id);

    public static bool operator ==(PositionId? left, PositionId? right)
    {
        return left is null ? right is null : left.Equals(right);
    }

    public static bool operator !=(PositionId? left, PositionId? right)
    {
        return !(left == right);
    }

    public static bool operator <(PositionId? left, PositionId? right)
    {
        return left is null ? right is not null : left.CompareTo(right) < 0;
    }

    public static bool operator <=(PositionId? left, PositionId? right)
    {
        return left is null || left.CompareTo(right) <= 0;
    }

    public static bool operator >(PositionId? left, PositionId? right)
    {
        return left is not null && left.CompareTo(right) > 0;
    }

    public static bool operator >=(PositionId? left, PositionId? right)
    {
        return left is null ? right is null : left.CompareTo(right) >= 0;
    }

    public static implicit operator Guid(PositionId positionId)
    {
        ArgumentNullException.ThrowIfNull(positionId);
        return positionId.Value;
    }

    public int CompareTo(PositionId? other)
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
        return Equals(obj as PositionId);
    }

    public bool Equals(PositionId? other)
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
