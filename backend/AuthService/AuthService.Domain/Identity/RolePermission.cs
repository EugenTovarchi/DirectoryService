using System.Diagnostics.CodeAnalysis;

namespace AuthService.Domain.Identity;

/// <summary>
/// Связь role-permission для расчета итоговых разрешений пользователя.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain term from AuthService documentation.")]
public sealed class RolePermission
{
    private RolePermission()
    {
    }

    public RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public ApplicationRole Role { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;
}
