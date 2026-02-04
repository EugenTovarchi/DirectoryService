using SharedService.SharedKernel;

namespace DirectoryService.Domain;

public abstract class SoftDeletableEntity<TId>(TId id) : Entity<TId>(id)
    where TId : IComparable<TId>
{
    public virtual void Delete()
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    public virtual void Restore()
    {
        if (!IsDeleted) return;

        IsDeleted = false;
        DeletedAt = null;
    }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
}
