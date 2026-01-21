using DirectoryService.SharedKernel;

namespace DirectoryService.Domain;

public abstract class SoftDeletableEntity<TId> : Entity<TId> where TId : IComparable<TId>
{
    protected SoftDeletableEntity(TId id) : base(id) { }

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
