namespace AuthService.Domain.Identity;

/// <summary>
/// Разрешение верхнего уровня, которое можно положить в JWT и проверять через policies.
/// </summary>
public sealed class Permission
{
    private Permission()
    {
    }

    public Permission(string code, string? description)
    {
        Id = Guid.NewGuid();
        Code = code;
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public ICollection<RolePermission> RolePermissions { get; } = [];
}
