using Microsoft.AspNetCore.Identity;

namespace AuthService.Domain.Identity;

/// <summary>
/// Identity-роль с описанием для admin UI и seed-документации.
/// </summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
    private ApplicationRole()
    {
    }

    public ApplicationRole(string name, string? description)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }

    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public ICollection<RolePermission> RolePermissions { get; } = [];
}
