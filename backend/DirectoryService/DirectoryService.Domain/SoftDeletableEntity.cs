using DirectoryService.SharedKernel;

namespace DirectoryService.Domain;

public abstract class SoftDeletableEntity<TId> : Entity<TId> where TId : IComparable<TId>
{
    protected SoftDeletableEntity(TId id) : base(id) { }

    public virtual void Delete()
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletionDate = DateTime.Now;
    }
    public virtual void Restor()
    {
        if (!IsDeleted) return;

        IsDeleted = false;
        DeletionDate = null;
    }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletionDate { get; private set; }
}
